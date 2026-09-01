namespace JbAutoAi;

/// The claim screen's two orienting widgets: where this claim is in the pipeline,
/// and what the handler should do next.
///
/// Both are derived from the rule gates that actually fired, not from a status
/// string. That matters — a "next action" invented separately from the decision
/// would drift out of step with it, and a handler following stale advice is worse
/// off than one following none.
public static class ClaimProgress
{
    public record Stage(string Key, string Label, string State, string? Detail);

    public record NextAction(string Severity, string Title, string Detail,
                             string? Href = null, string? ButtonKey = null);

    // done | active | blocked | pending
    public static List<Stage> Stages(Claim c, IReadOnlyList<Document> docs,
                                     IReadOnlyList<Rules.Reason> reasons)
    {
        bool Failed(string code) => reasons.Any(r => r.Code == code && !r.Ok);
        var analysed = c.DecisionOutcome is not null;

        var photos = docs.Count(d => d.DocType == "photo");
        var estimates = docs.Count(d => d.DocType == "repair_estimate");
        var evidenceOk = photos > 0 && estimates > 0;

        var stages = new List<Stage>
        {
            new("intake", I18n.T("stage.intake"),
                Failed("FNOL_COMPLETE") ? "blocked" : "done",
                c.ClaimNumber),

            new("evidence", I18n.T("stage.evidence"),
                docs.Count == 0 ? "pending" : evidenceOk ? "done" : "active",
                I18n.T("stage.evidenceDetail", docs.Count, photos, estimates)),

            new("extraction", I18n.T("stage.extraction"),
                !analysed ? "pending" : Failed("EXTRACTION_CONFIDENT") ? "active" : "done",
                c.ExtractionConfidence is { } e ? e.ToString("P0") : null),

            new("fraud", I18n.T("stage.fraud"),
                !analysed ? "pending" : Failed("FRAUD_SIGNALS_LOW") ? "blocked" : "done",
                c.FraudScore.ToString("F2")),

            new("legal", I18n.T("stage.legal"),
                !analysed ? "pending" : Failed("CITATIONS_RESOLVABLE") ? "blocked" : "done",
                c.CitationIntegrity is { } ci ? ci.ToString("P0") : null),

            new("decision", I18n.T("stage.decision"),
                !analysed ? "pending" : c.DecisionOutcome == "auto_approved" ? "done" : "active",
                analysed ? I18n.T("decision." + c.DecisionOutcome switch
                {
                    "auto_approved" => "auto",
                    "assisted" => "assisted",
                    _ => "manual",
                }) : null),
        };
        return stages;
    }

    /// Failed gate → the concrete thing a handler does about it. Hard blockers
    /// first, because those are the ones that cannot be waved through.
    static readonly Dictionary<string, (int Rank, string Severity, string Key, string? Href, string? Button)> Playbook =
        new()
        {
            ["NO_PERSONAL_INJURY"]    = (0, "stop", "act.injury", null, null),
            ["WITHIN_LIMITATION"]     = (1, "stop", "act.limitation", null, null),
            ["FRAUD_SIGNALS_LOW"]     = (2, "stop", "act.fraud", "#fraud", "act.btn.escalate"),
            ["CITATIONS_RESOLVABLE"]  = (3, "stop", "act.citations", "/legal", "act.btn.legal"),
            ["NOT_TOTAL_LOSS"]        = (4, "stop", "act.totalLoss", null, null),
            ["LOSS_DATE_VALID"]       = (5, "stop", "act.lossDate", null, null),
            ["AI_SERVICES_HEALTHY"]   = (6, "warn", "act.degraded", null, "act.btn.reanalyse"),
            ["EVIDENCE_MINIMUM"]      = (7, "warn", "act.evidence", "#correspondence", "act.btn.request"),
            ["EXTRACTION_CONFIDENT"]  = (8, "warn", "act.confidence", null, null),
            ["AMOUNT_WITHIN_CAP"]     = (9, "warn", "act.amount", null, null),
            ["NO_THIRD_PARTY_DISPUTE"] = (10, "warn", "act.thirdParty", null, null),
            ["NOTIFICATION_TIMELY"]   = (11, "warn", "act.late", null, null),
            ["VEHICLE_ID_FORMAT"]     = (12, "warn", "act.vehicleId", null, null),
            ["FNOL_COMPLETE"]         = (13, "warn", "act.fnol", null, null),
        };

    public static List<NextAction> NextActions(Claim c, IReadOnlyList<Rules.Reason> reasons)
    {
        if (reasons.Count == 0)
            return [new NextAction("clay", I18n.T("act.analyse"), I18n.T("act.analyseDetail"),
                                   null, "act.btn.analyse")];

        var actions = reasons
            .Where(r => !r.Ok && Playbook.ContainsKey(r.Code))
            .Select(r => (r, p: Playbook[r.Code]))
            .OrderBy(x => x.p.Rank)
            .Select(x => new NextAction(x.p.Severity, I18n.T(x.p.Key), x.r.Message,
                                        x.p.Href, x.p.Button))
            .ToList();

        if (actions.Count == 0)
            actions.Add(new NextAction("ok", I18n.T("act.clear"), I18n.T("act.clearDetail")));

        return actions;
    }

    /// 0..1 fill for the amount-versus-cap bar; >1 means over the ceiling.
    public static double CapRatio(Claim c) =>
        c.EstimatedAmountEur is { } a && Rules.Cap() > 0 ? a / Rules.Cap() : 0;
}
