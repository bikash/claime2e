using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JbAutoAi;

public static class Json
{
    /// snake_case everywhere: the DB, the public API and the audit export all read
    /// the same field names, so an exported decision is diffable against the log.
    public static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// Same shape, but the default encoder escapes < > &. Anything that lands
    /// inside a <script> block must use this: a policyholder called
    /// `</script><script>…` would otherwise close the tag and execute.
    public static readonly JsonSerializerOptions HtmlSafeOpts = new(Opts)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default,
    };

    public static string Str(object? value) => JsonSerializer.Serialize(value, Opts);

    public static string ForScriptTag(object? value) => JsonSerializer.Serialize(value, HtmlSafeOpts);

    public static T? Parse<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, Opts);
}

/// FR-10 — the end-to-end pipeline.
///
/// FNOL → ingest → classify → extract → summarise → coverage/liability → severity
/// → fraud screen → decision → routing. Idempotent: re-running analysis on a claim
/// recomputes from the documents on file and replaces the decision.
public static class Pipeline
{
    public static string UploadsRoot { get; set; } = "uploads";

    // --- ingest -----------------------------------------------------------------

    public static async Task IngestFileAsync(Claim claim, string filename, byte[] raw,
                                             string? portalUserId = null)
    {
        var claimDir = Path.Combine(UploadsRoot, claim.Id);
        Directory.CreateDirectory(claimDir);

        var contentHash = Media.Sha256Hex(raw);
        var safeName = $"{contentHash[..8]}_{Path.GetFileName(filename)}";
        var target = Path.Combine(claimDir, safeName);
        await File.WriteAllBytesAsync(target, raw);

        var kind = Media.ClassifyByExtension(filename);
        var docType = kind;
        string? perceptualHash = null;
        JsonNode? extracted = null;
        var degraded = new List<string>();

        switch (kind)
        {
            case "pdf":
            case "email":
            {
                var text = kind == "pdf" ? Media.PdfText(raw) : System.Text.Encoding.UTF8.GetString(raw);
                var cls = await SafeAsync(
                    async () => (await Llm.ClassifyDocumentAsync(text, filename, claim.Id)).DocType,
                    kind == "pdf" ? "other" : "email", "classify", degraded);
                docType = string.IsNullOrWhiteSpace(cls) ? kind : cls;
                extracted = await SafeAsync<JsonNode?>(
                    async () => await Llm.ExtractFromTextAsync(text, docType, claim.Id),
                    null, "extract", degraded);
                break;
            }
            case "photo":
            {
                perceptualHash = Media.PerceptualHash(raw);
                extracted = await SafeAsync<JsonNode?>(
                    async () => await Llm.AnalyzeDamageImageAsync(target, claim.Id),
                    null, "vision", degraded);
                var exif = Media.PhotoExifSignals(raw, claim.LossDate);
                if (exif.Count > 0)
                {
                    extracted ??= new JsonObject();
                    extracted["photo_signals"] = JsonNode.Parse(Json.Str(exif));
                }
                break;
            }
        }

        // An upload never fails because a model was unreachable; the gap surfaces
        // on the claim and analysis withholds auto-approval.
        if (degraded.Count > 0)
        {
            extracted ??= new JsonObject();
            extracted["ai_degraded"] = JsonNode.Parse(Json.Str(degraded));
        }

        Db.AddDocument(claim.Id, filename, Path.Combine(claim.Id, safeName).Replace('\\', '/'),
                       docType, contentHash, perceptualHash, extracted?.ToJsonString(), portalUserId);
    }

    // --- analyse ----------------------------------------------------------------

    static double? Num(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;

    /// Model JSON is untrusted shape as well as untrusted content — a field the
    /// schema declares as a bool can come back as "true", 1, or absent.
    static bool Flag(JsonNode? n) => n?.GetValueKind() switch
    {
        System.Text.Json.JsonValueKind.True => true,
        System.Text.Json.JsonValueKind.String => bool.TryParse(n.ToString(), out var b) && b,
        System.Text.Json.JsonValueKind.Number => Num(n) is > 0,
        _ => false,
    };

    /// NFR-6 graceful degradation: an AI service that is down must not abort a
    /// claim. The stage falls back, the failure is recorded on the claim, and the
    /// rules engine withholds auto-approval because the evidence is incomplete.
    static async Task<T> SafeAsync<T>(Func<Task<T>> call, T fallback, string stage, List<string> degraded)
    {
        try
        {
            return await call();
        }
        catch (Exception e)
        {
            degraded.Add($"{stage}: {e.Message}");
            Console.Error.WriteLine($"[degraded] {stage}: {e.Message}");
            return fallback;
        }
    }

    public static async Task<string> AnalyseAsync(string claimId)
    {
        var claim = Db.GetClaim(claimId) ?? throw new InvalidOperationException("claim not found");
        var docs = Db.GetDocuments(claimId);

        // 1. Merge extracted signals across documents.
        var degraded = new List<string>();
        List<double> amounts = [], confidences = [];
        List<string> damageCategories = [], severitySeen = [];
        List<Rules.Signal> photoSignals = [];
        var duplicateHits = 0;
        int photoCount = 0, estimateCount = 0;
        var policeReportPresent = false;
        string? impactDirection = null;
        var poorPhotoQuality = 0;
        bool injuriesFromDocs = false, thirdPartyFromDocs = false;

        foreach (var d in docs)
        {
            if (d.DocType == "photo") photoCount++;
            if (d.DocType == "repair_estimate") estimateCount++;
            if (d.DocType == "police_report") policeReportPresent = true;

            if (JsonNode.Parse(d.Extracted ?? "null") is JsonObject ex)
            {
                if (ex["estimated_amount_eur"] is JsonObject amt)
                {
                    if (Num(amt["value"]) is > 0 and var v) amounts.Add(v);
                    if (Num(amt["confidence"]) is { } c) confidences.Add(c);
                }
                if (Num(ex["overall_confidence"]) is { } oc) confidences.Add(oc);
                if (Num(ex["confidence"]) is { } cc) confidences.Add(cc);

                if (ex["damage_areas"] is JsonArray areas)
                    damageCategories.AddRange(areas.Select(a => a?.ToString() ?? "").Where(s => s.Length > 0));

                if (ex["severity"]?.ToString() is { Length: > 0 } sev)
                {
                    severitySeen.Add(sev);
                    if (sev == "total_loss") damageCategories.Add("total_loss");
                }

                if (ex["estimated_repair_range_eur"] is JsonArray { Count: 2 } range
                    && Num(range[0]) is { } lo && Num(range[1]) is { } hi)
                    amounts.Add((lo + hi) / 2);

                if (ex["impact_direction"]?.ToString() is { Length: > 0 } dir && dir != "unknown")
                    impactDirection = dir;
                if (ex["photo_quality"]?.ToString() == "poor") poorPhotoQuality++;

                // Injury and third-party signals found *in the documents*, not just
                // on the FNOL form. Tuned for recall on purpose: missing an injury
                // and auto-settling is the severe failure, a false positive only
                // costs a handler touch. Latched on, never off.
                if (Flag(ex["injuries_mentioned"]?["value"])) injuriesFromDocs = true;
                if (Flag(ex["third_party_mentioned"]?["value"])) thirdPartyFromDocs = true;

                // A model that was unreachable at upload time leaves a marker here.
                if (ex["ai_degraded"] is JsonArray ad)
                    degraded.AddRange(ad.Select(x => $"{d.Filename}: {x}"));

                if (ex["photo_signals"] is JsonArray ps)
                    foreach (var s in ps)
                        if (s is JsonObject so)
                            photoSignals.Add(new(so["code"]?.ToString() ?? "PHOTO_SIGNAL",
                                                 so["severity"]?.ToString() ?? "low",
                                                 so["message"]?.ToString() ?? ""));
            }

            if (d.PerceptualHash is { Length: > 0 })
                duplicateHits += Db.FindDuplicatePhotoClaims(d.PerceptualHash, claimId).Count;
        }

        var estimated = amounts.Count > 0 ? amounts.Max() : (double?)null;
        var overallConfidence = confidences.Count > 0 ? confidences.Average() : (double?)null;
        var damage = damageCategories.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();

        string[] severityOrder = ["none", "minor", "moderate", "severe", "total_loss"];
        var worstSeverity = severityOrder.LastOrDefault(severitySeen.Contains) ?? severitySeen.FirstOrDefault();

        // 2. Latch document-derived injury / third-party signals onto the claim
        //    before anything is scored. This is the FR-4.3 hard gate: an injury the
        //    FNOL form missed but a police report mentions must still block STP.
        var escalations = new List<string>();
        if (injuriesFromDocs && !claim.Injuries)
        {
            claim.Injuries = true;
            escalations.Add("personal injury detected in the documents (WVW 185 / BW 6:162)");
        }
        if (thirdPartyFromDocs && !claim.ThirdPartyInvolved)
        {
            claim.ThirdPartyInvolved = true;
            escalations.Add("third-party involvement detected in the documents (WAM)");
        }
        if (escalations.Count > 0)
        {
            Db.SetDerivedFlags(claimId, claim.Injuries, claim.ThirdPartyInvolved);
            Db.AddActivity(claimId, "note", null,
                "Extraction escalated the claim: " + string.Join("; ", escalations) + ".",
                Json.Str(new { injuries = claim.Injuries, third_party_involved = claim.ThirdPartyInvolved }));
        }

        // 3. Fraud screen against the merged view.
        claim.EstimatedAmountEur = estimated ?? claim.EstimatedAmountEur;
        claim.ExtractionConfidence = overallConfidence ?? claim.ExtractionConfidence;
        var fraud = Rules.ComputeFraud(claim, docs, duplicateHits, photoSignals);

        // 3. Persist the merged state, then reload so downstream stages see the DB truth.
        Db.UpdateClaimAnalysis(claimId, estimated, overallConfidence, fraud.Score,
                               Json.Str(damage), null, "analyzed");
        claim = Db.GetClaim(claimId)!;

        // 4. Retrieve the law in force on the incident date (FR-11).
        var asOf = claim.LossDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var query = Legal.BuildQuery(claim, docs);
        var retrieved = await SafeAsync(
            () => Legal.RetrieveAsync(query, asOf, Legal.DefaultTopK, claimId: claimId),
            [], "legal_retrieval", degraded);

        // 5. Provisional rules pass — narrative context for the model only. The
        //    binding evaluation happens after citations are verified.
        var provisional = Rules.Evaluate(claim, docs, fraud.Score);

        // 6. RAG-grounded liability analysis + summary.
        var liability = await SafeAsync<JsonNode>(
            () => Llm.AnalyseLiabilityAsync(claim, docs, retrieved, claimId),
            new JsonObject(), "liability", degraded);
        var summary = await SafeAsync<string?>(
            async () => await Llm.SummariseClaimAsync(claim, docs, provisional, retrieved, claimId),
            null, "summary", degraded);

        // 7. Citation verification — the hallucination gate.
        var basisIds = (liability["legal_basis"] as JsonArray)?
            .Select(b => b?["chunk_id"]?.ToString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => $"[[cite:{s}]]") ?? [];
        var audit = Legal.VerifyCitations(retrieved, summary,
                                          liability["reasoning"]?.ToString(),
                                          string.Join(" ", basisIds));

        var corpusVersion = Db.ActiveCorpusVersion() ?? "none";
        Db.ReplaceClaimCitations(claimId, audit.Citations, corpusVersion, audit.Integrity);

        // 8. Binding decision.
        var result = Rules.Evaluate(claim, docs, fraud.Score, audit.Integrity, degraded.Count == 0);

        Db.UpdateClaimAnalysis(claimId, null, null, null, null, summary, "analyzed");
        Db.RecordDecision(claimId, result.Outcome, Json.Str(result.Reasons), Json.Str(result.Trace));

        var assessment = new Dictionary<string, object?>
        {
            ["damage_areas"] = damage,
            ["severity"] = worstSeverity,
            ["impact_direction"] = impactDirection,
            ["estimated_amount_eur"] = estimated,
            ["extraction_confidence"] = overallConfidence,
            ["photo_count"] = photoCount,
            ["poor_quality_photos"] = poorPhotoQuality,
            ["estimate_document_count"] = estimateCount,
            ["police_report_present"] = policeReportPresent,
            ["evidence_count"] = docs.Count,
            ["fraud_score"] = fraud.Score,
            ["liability"] = JsonSerializer.Deserialize<JsonElement>(liability.ToJsonString()),
            ["ai_degraded"] = degraded,
            ["legal"] = new Dictionary<string, object?>
            {
                ["corpus_version"] = corpusVersion,
                ["retrieved"] = retrieved.Count,
                ["cited"] = audit.Emitted,
                ["unresolved"] = audit.Unresolved,
                ["integrity"] = audit.Integrity,
                ["as_of"] = asOf.ToString("yyyy-MM-dd"),
                ["query"] = query,
            },
        };
        Db.RecordFraudAndAssessment(claimId, Json.Str(fraud.Signals), Json.Str(assessment));

        if (degraded.Count > 0)
            Db.AddActivity(claimId, "note", null,
                $"AI services degraded during analysis ({degraded.Count} stage(s)); auto-approval withheld.",
                Json.Str(new { ai_degraded = degraded }));

        // 9. Auto-routing on top of the decision.
        AutoRoute(claim, fraud.Score, liability, audit);

        // Customer-facing status. Deliberately carries the outcome token, not prose:
        // the portal renders it in the reader's language, and nothing internal
        // (fraud score, citations, routing) crosses the line.
        Db.AddCustomerActivity(claimId, "status", null, result.Outcome);

        Db.AddActivity(claimId, "decision", null,
            $"Rules engine decision: {result.Outcome}.",
            Json.Str(new Dictionary<string, object?>
            {
                ["outcome"] = result.Outcome,
                ["fraud_score"] = fraud.Score,
                ["estimated_amount_eur"] = estimated,
                ["citation_integrity"] = audit.Integrity,
                ["corpus_version"] = corpusVersion,
            }));

        return result.Outcome;
    }

    /// Roles the pipeline hands claims to. An assignment to anything else was made
    /// by a person and is left alone.
    static readonly HashSet<string> AutoAssignedRoles =
        ["injury_department", "fraud_specialist", "liability_department", "senior_adjuster"];

    static void AutoRoute(Claim claim, double fraudScore, JsonNode liability, Legal.CitationAudit audit)
    {
        (string Role, string Reason)? route = null;

        if (claim.Injuries)
            route = ("injury_department",
                     "Personal injury reported (WVW 185 / BW 6:162) — routed to injury department.");
        else if (audit.Unresolved > 0)
            route = ("senior_adjuster",
                     $"{audit.Unresolved} legal citation(s) did not resolve against the corpus — "
                   + "decision cannot be released without human verification.");
        else if (fraudScore >= 0.3)
            route = ("fraud_specialist", $"Fraud score {fraudScore:F2} ≥ 0.30 — routed to fraud specialist.");
        // Only trust the liability flag when a real analysis ran; the stub always
        // returns requires_human = true, which would route every claim.
        else if (!Llm.Stubbed() && Flag(liability["requires_human"]))
            route = ("liability_department",
                     "Liability analysis flagged a mandatory-human category — routed to liability department.");
        else if (claim.ThirdPartyInvolved)
            route = ("liability_department", "Third-party involvement (WAM) — routed to liability department.");

        if (route is not { } r) return;
        var candidate = Db.HandlersByRole(r.Role).FirstOrDefault();
        if (candidate is null) return;

        // Re-analysis must not undo a human. If someone has already taken or
        // reassigned the claim, auto-routing stays out of it — unless the claim has
        // escalated into a different department, which is worth overriding for.
        var current = Db.GetHandler(claim.AssignedHandlerId);
        if (current is not null && current.Role == r.Role) return;
        if (current is not null && !AutoAssignedRoles.Contains(current.Role)) return;

        Db.AssignClaim(claim.Id, candidate.Id);
        Db.AddActivity(claim.Id, "delegated", null,
            $"Auto-routed to {candidate.Name} ({r.Role.Replace('_', ' ')}). {r.Reason}",
            Json.Str(new Dictionary<string, object?>
            {
                ["handler_id"] = candidate.Id,
                ["handler_name"] = candidate.Name,
                ["role"] = r.Role,
                ["reason"] = r.Reason,
                ["automatic"] = true,
            }));
    }
}
