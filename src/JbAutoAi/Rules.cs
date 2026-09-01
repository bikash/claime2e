using System.Text.RegularExpressions;

namespace JbAutoAi;

/// Deterministic Dutch motor-claim rules engine.
///
/// Legal anchors are advisory and MUST be reviewed by counsel before production.
/// Each rule carries the citation key of a legal_refs row so the workspace can
/// show the source article behind the gate (see Legal.cs).
///
///  - WAM art. 2/3   mandatory motor liability insurance and its scope
///  - WAM art. 22    statutory minimum insured sums
///  - BW  art. 3:310 limitation: 5 years from knowledge, 20 years absolute
///  - BW  art. 6:98  causation / attribution of damage
///  - BW  art. 6:162 unlawful act
///  - BW  art. 7:941 duty to notify the insurer as soon as reasonably possible
///  - WVW art. 185   strict liability of the motorist toward non-motorised users
///
/// The engine NEVER auto-denies. Denials always route to a human.
public static partial class Rules
{
    [GeneratedRegex("^[A-HJ-NPR-Z0-9]{17}$")] private static partial Regex VinRe();
    [GeneratedRegex("^[A-Z0-9]{5,8}$")] private static partial Regex PlateRe();   // normalised, dashes stripped

    public record Reason(string Code, bool Ok, string Message, string? LegalRef = null);

    public record RulesResult(string Outcome, List<Reason> Reasons, Dictionary<string, object?> Trace);

    /// Weight is what this signal contributed to the composite score. Carrying it
    /// on the signal is what makes FR-7.4's "transparent signal breakdown" possible
    /// — the workspace can show the arithmetic instead of a bare number.
    public record Signal(string Code, string Severity, string Message, double Weight = 0);

    public static double SeverityWeight(string severity) =>
        severity switch { "high" => 0.4, "medium" => 0.2, _ => 0.05 };

    public record FraudResult(double Score, List<Signal> Signals);

    static readonly HashSet<string> HardBlockers =
    [
        "NO_PERSONAL_INJURY", "WITHIN_LIMITATION", "FRAUD_SIGNALS_LOW", "LOSS_DATE_VALID",
        "NOT_TOTAL_LOSS", "CITATIONS_RESOLVABLE",
    ];

    public static double Cap() => EnvDouble("AUTO_APPROVE_CAP_EUR", 2500);
    public static double ConfidenceMin() => EnvDouble("EXTRACTION_CONFIDENCE_MIN", 0.85);

    static double EnvDouble(string key, double fallback) =>
        double.TryParse(Environment.GetEnvironmentVariable(key),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public static RulesResult Evaluate(Claim claim, IReadOnlyList<Document> documents, double fraudScore,
                                       double citationIntegrity = 1.0, bool aiServicesHealthy = true)
    {
        var reasons = new List<Reason>();
        var trace = new Dictionary<string, object?>();

        // 1. FNOL completeness.
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(claim.PolicyholderName)) missing.Add("policyholder_name");
        if (string.IsNullOrWhiteSpace(claim.PolicyNumber)) missing.Add("policy_number");
        if (string.IsNullOrWhiteSpace(claim.LicensePlate)) missing.Add("license_plate");
        if (claim.LossDate is null) missing.Add("loss_date");
        if (string.IsNullOrWhiteSpace(claim.LossLocation)) missing.Add("loss_location");
        if (string.IsNullOrWhiteSpace(claim.Description)) missing.Add("description");
        trace["missing_fnol_fields"] = missing;
        reasons.Add(new("FNOL_COMPLETE", missing.Count == 0,
            missing.Count == 0
                ? "All required FNOL fields present."
                : $"Missing FNOL fields: {string.Join(", ", missing)}."));

        // 2. Plate / VIN format.
        var plate = Db.NormalisePlate(claim.LicensePlate);
        var vin = (claim.Vin ?? "").ToUpperInvariant();
        var plateOk = plate.Length > 0 && PlateRe().IsMatch(plate);
        var vinOk = vin.Length == 0 || VinRe().IsMatch(vin);   // VIN optional at FNOL
        trace["plate"] = plate;
        trace["vin"] = vin;
        reasons.Add(new("VEHICLE_ID_FORMAT", plateOk && vinOk,
            plateOk && vinOk
                ? "Plate/VIN format valid."
                : "Plate or VIN has unexpected format. Adjuster review."));

        // 3. Loss date, notification window, limitation.
        var loss = claim.LossDate;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int? daysSince = loss is null ? null : today.DayNumber - loss.Value.DayNumber;
        trace["days_since_loss"] = daysSince;

        var dateOk = loss is not null && loss <= today;
        reasons.Add(new("LOSS_DATE_VALID", dateOk,
            dateOk ? "Loss date parseable and not in the future." : "Loss date missing or in the future.",
            "BW 3:310"));

        var withinNotice = daysSince is >= 0 and <= 30;
        reasons.Add(new("NOTIFICATION_TIMELY", withinNotice,
            daysSince is not null
                ? $"Reported {daysSince} days after loss."
                : "Cannot determine notification delay.",
            "BW 7:941"));

        var limitationOk = daysSince is not null && daysSince <= 5 * 365;
        reasons.Add(new("WITHIN_LIMITATION", limitationOk,
            limitationOk
                ? "Within 5-year limitation window."
                : "Outside 5-year limitation (BW 3:310). Manual review.",
            "BW 3:310"));

        // 4. Injuries / third-party liability.
        trace["injuries"] = claim.Injuries;
        trace["third_party_involved"] = claim.ThirdPartyInvolved;
        reasons.Add(new("NO_PERSONAL_INJURY", !claim.Injuries,
            !claim.Injuries
                ? "No personal injury reported."
                : "Personal injury reported — WVW 185 / BW 6:162 exposure. Manual review.",
            "WVW 185"));
        reasons.Add(new("NO_THIRD_PARTY_DISPUTE", !claim.ThirdPartyInvolved,
            !claim.ThirdPartyInvolved
                ? "First-party only."
                : "Third-party involved — WAM liability path. Assisted review.",
            "WAM 3"));

        // 5. Amount envelope.
        var amount = claim.EstimatedAmountEur;
        var cap = Cap();
        trace["estimated_amount_eur"] = amount;
        trace["auto_approve_cap_eur"] = cap;
        var amountOk = amount is > 0 && amount <= cap;
        reasons.Add(new("AMOUNT_WITHIN_CAP", amountOk,
            amountOk
                ? $"Estimated EUR {amount:F2} within auto-approve cap EUR {cap:F2}."
                : $"Estimated amount EUR {(amount?.ToString("F2") ?? "—")} exceeds cap EUR {cap:F2} or is missing."));

        // 6. Extraction confidence.
        var conf = claim.ExtractionConfidence;
        var confMin = ConfidenceMin();
        trace["extraction_confidence"] = conf;
        trace["extraction_confidence_min"] = confMin;
        var confOk = conf is not null && conf >= confMin;
        reasons.Add(new("EXTRACTION_CONFIDENT", confOk,
            confOk
                ? $"Extraction confidence {conf:F2} ≥ threshold {confMin:F2}."
                : $"Extraction confidence {(conf?.ToString("F2") ?? "—")} below threshold {confMin:F2}."));

        // 7. Fraud signals.
        trace["fraud_score"] = fraudScore;
        var fraudOk = fraudScore < 0.3;
        reasons.Add(new("FRAUD_SIGNALS_LOW", fraudOk,
            fraudOk
                ? $"Fraud score {fraudScore:F2} below 0.30 threshold."
                : $"Fraud signals present (score {fraudScore:F2}). Manual review."));

        // 8. Total loss.
        var totalLoss = (claim.DamageCategories ?? "").Contains("total_loss");
        trace["total_loss_flagged"] = totalLoss;
        reasons.Add(new("NOT_TOTAL_LOSS", !totalLoss,
            !totalLoss
                ? "Not classified as total loss."
                : "Total loss classified — requires salvage/valuation review (WOK / RDW)."));

        // 9. Citation integrity (FR-11). A legal basis the corpus cannot resolve is
        // an unverifiable decision, so it blocks STP and goes to a handler.
        trace["citation_integrity"] = citationIntegrity;
        var citationsOk = citationIntegrity >= 1.0;
        reasons.Add(new("CITATIONS_RESOLVABLE", citationsOk,
            citationsOk
                ? "Every legal citation resolves to a retrieved corpus passage."
                : $"Citation integrity {citationIntegrity:P0} — at least one legal reference could not be "
                + "resolved against the retrieved corpus. Manual review.",
            "NFR-2 audit"));

        // 10. AI service health (NFR-6). A stage that fell back means the case file
        // is incomplete, so auto-approval is withheld — but the claim still gets a
        // decision and a handler, rather than an error page.
        trace["ai_services_healthy"] = aiServicesHealthy;
        reasons.Add(new("AI_SERVICES_HEALTHY", aiServicesHealthy,
            aiServicesHealthy
                ? "All AI stages completed."
                : "One or more AI stages degraded to fallback. Handler review before release."));

        // 11. Supporting evidence.
        var hasPhoto = documents.Any(d => d.DocType == "photo");
        var hasEstimate = documents.Any(d => d.DocType == "repair_estimate");
        reasons.Add(new("EVIDENCE_MINIMUM", hasPhoto && hasEstimate,
            hasPhoto && hasEstimate
                ? "At least one photo and one repair estimate on file."
                : "Missing photo and/or repair estimate. Adjuster to request."));

        var allPass = reasons.All(r => r.Ok);
        var hardFail = reasons.Any(r => !r.Ok && HardBlockers.Contains(r.Code));
        var outcome = allPass ? "auto_approved" : hardFail ? "manual" : "assisted";

        return new RulesResult(outcome, reasons, trace);
    }

    /// Additive fraud scoring. Score ≥ 0.30 blocks auto-approval via FRAUD_SIGNALS_LOW.
    /// Fraud indicators are investigation signals only — never an autonomous denial
    /// (Verbond van Verzekeraars / PIFI).
    ///
    /// ponytail: additive heuristic. Upgrade path — swap for a trained classifier
    /// once leakage / false-approval telemetry justifies it.
    public static FraudResult ComputeFraud(Claim claim, IReadOnlyList<Document> documents,
                                           int duplicatePhotoHits,
                                           IReadOnlyList<Signal>? photoSignals = null)
    {
        var signals = new List<Signal>();
        var score = 0.0;

        // 1. Recycled photo across prior claims — strongest single signal.
        if (duplicatePhotoHits > 0)
        {
            score += 0.6;
            signals.Add(new("PHOTO_RECYCLED", "high",
                $"Photo perceptual-hash matched {duplicatePhotoHits} document(s) in prior claim(s).", 0.6));
        }

        // 2. Delayed reporting.
        if (claim.LossDate is { } loss)
        {
            var days = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - loss.DayNumber;
            if (days > 180)
            {
                score += 0.2;
                signals.Add(new("REPORT_VERY_LATE", "medium",
                    $"Reported {days} days after loss (>180d).", 0.2));
            }
            else if (days > 60)
            {
                score += 0.1;
                signals.Add(new("REPORT_LATE", "low",
                    $"Reported {days} days after loss (>60d).", 0.1));
            }
        }

        // 3. No supporting evidence at all.
        if (documents.Count == 0)
        {
            score += 0.15;
            signals.Add(new("NO_EVIDENCE", "medium", "No documents or photos submitted.", 0.15));
        }

        // 4. Layered risk: injury plus third party.
        if (claim.Injuries && claim.ThirdPartyInvolved)
        {
            score += 0.1;
            signals.Add(new("INJURY_THIRD_PARTY_LAYERED", "medium",
                "Personal injury AND third-party liability reported — layered exposure.", 0.1));
        }

        // 5. Photo-level signals surfaced at upload (EXIF mismatch, no EXIF).
        //    Collapsed by code: ten photos with no EXIF is one anomaly seen ten
        //    times, not ten anomalies. Charging per photo made the score a function
        //    of how many pictures the claimant happened to send.
        foreach (var group in (photoSignals ?? []).GroupBy(p => p.Code))
        {
            var worst = group.OrderByDescending(p => SeverityWeight(p.Severity)).First();
            var weight = SeverityWeight(worst.Severity);
            var count = group.Count();
            score += weight;
            signals.Add(worst with
            {
                Weight = weight,
                Message = count == 1 ? worst.Message : $"{worst.Message} ({count} photos affected)",
            });
        }

        // 6. Very high estimate with low extraction confidence.
        var amount = claim.EstimatedAmountEur ?? 0;
        var conf = claim.ExtractionConfidence ?? 0;
        if (amount > 5000 && conf > 0 && conf < 0.6)
        {
            score += 0.1;
            signals.Add(new("AMOUNT_HIGH_CONF_LOW", "medium",
                $"Estimate €{amount:F0} with low extraction confidence ({conf:F2}).", 0.1));
        }

        return new FraudResult(Math.Min(score, 1.0), signals);
    }
}
