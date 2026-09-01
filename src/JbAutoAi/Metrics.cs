namespace JbAutoAi;

/// Appendix A of the dashboard spec, in code.
///
/// Every dashboard KPI resolves through this class, so the Handler, Team Manager and
/// CFO views cannot disagree about what a number means (NFR-3 / CR-1), and the
/// definition shown on hover comes from the same record the query is built from, so
/// the on-screen text and the spec cannot drift (CR-9).
///
/// A KPI is a (Definition, Scope) pair: the definition says what it means, the scope
/// says which claims are in it. Drill-down re-runs the *same* scope, which is how a
/// card and the list behind it are guaranteed to agree (CR-4).
public static class Metrics
{
    /// Configured thresholds. One place, because they appear in a metric, a colour
    /// and a target line, and three copies would drift.
    public const int SlaWorkingDays = 5;        // SLA breach threshold, spec §4.3
    public const int HandlerCapacity = 20;      // open cases counted as 100% load, MD-2
    public const double OverloadAt = 0.85;      // MD-2 flags above this
    public const double StpTarget = 0.55;       // MD-1 target line
    public const int HandlerDailyTarget = 4;    // HD-3 target line

    /// A metric definition. Formula and Source are i18n keys so CR-6 holds for the
    /// hover text too.
    public record Def(string Key, string LabelKey, string FormulaKey, string SourceKey,
                      bool Available = true, string? MissingSourceKey = null);

    public static readonly Dictionary<string, Def> Definitions = new()
    {
        // handler
        ["open_cases"] = new("open_cases", "m.openCases", "m.openCases.f", "src.claimStore"),
        ["due_today"] = new("due_today", "m.dueToday", "m.dueToday.f", "src.slaEngine"),
        ["stp_ready"] = new("stp_ready", "m.stpReady", "m.stpReady.f", "src.decisionEngine"),
        ["handling_time"] = new("handling_time", "m.handlingTime", "m.handlingTime.f", "src.claimStore",
                                Available: false, MissingSourceKey: "miss.handlingTime"),
        // manager
        ["stp_rate"] = new("stp_rate", "m.stpRate", "m.stpRate.f", "src.decisionEngine"),
        ["backlog"] = new("backlog", "m.backlog", "m.backlog.f", "src.claimStore"),
        ["sla_breaches"] = new("sla_breaches", "m.slaBreaches", "m.slaBreaches.f", "src.slaEngine"),
        ["cycle_time"] = new("cycle_time", "m.cycleTime", "m.cycleTime.f", "src.claimStore"),
        ["to_siu"] = new("to_siu", "m.toSiu", "m.toSiu.f", "src.fraudEngine"),
        // cfo
        ["incurred"] = new("incurred", "m.incurred", "m.incurred.f", "src.claimStore"),
        ["outstanding"] = new("outstanding", "m.outstanding", "m.outstanding.f", "src.claimStore"),
        ["loss_ratio"] = new("loss_ratio", "m.lossRatio", "m.lossRatio.f", "src.ledgerPolicy",
                             Available: false, MissingSourceKey: "miss.premium"),
        ["recoveries"] = new("recoveries", "m.recoveries", "m.recoveries.f", "src.recovery",
                             Available: false, MissingSourceKey: "miss.recovery"),
        ["leakage"] = new("leakage", "m.leakage", "m.leakage.f", "src.fraudReserve",
                          Available: false, MissingSourceKey: "miss.leakage"),
        ["reserve_accuracy"] = new("reserve_accuracy", "m.reserveAccuracy", "m.reserveAccuracy.f", "src.claimStore",
                                   Available: false, MissingSourceKey: "miss.finalCost"),
        ["automation_savings"] = new("automation_savings", "m.automationSavings", "m.automationSavings.f", "src.claimStore",
                                     Available: false, MissingSourceKey: "miss.baselineCost"),
        ["cost_breakdown"] = new("cost_breakdown", "m.costBreakdown", "m.costBreakdown.f", "src.ledger",
                                 Available: false, MissingSourceKey: "miss.costComponents"),
    };

    /// One KPI tile: what it means (DefKey), what it says (Value), and which population
    /// its drill-down replays (Scope). Rendered by Pages/Shared/_Kpi.cshtml.
    public record KpiCard(string DefKey, string Value, string? Foot = null, string? Scope = null,
                          string Tone = "ink", Range? Period = null);

    public static Def Definition(string key) =>
        Definitions.TryGetValue(key, out var d) ? d : new Def(key, key, key, "src.claimStore");

    /// The dashboard's date window (CR-2). Default is the last 90 days.
    public record Range(DateOnly From, DateOnly To)
    {
        public static Range Default(DateOnly today) => new(today.AddDays(-89), today);

        /// Same length, immediately before this one — the comparison window for every
        /// "trend vs prior period" note in the spec.
        public Range Prior()
        {
            var days = To.DayNumber - From.DayNumber + 1;
            return new Range(From.AddDays(-days), From.AddDays(-1));
        }

        public static Range Parse(string? from, string? to, DateOnly today)
        {
            var f = DateOnly.TryParse(from, out var pf) ? pf : today.AddDays(-89);
            var t = DateOnly.TryParse(to, out var pt) ? pt : today;
            return t < f ? new Range(t, f) : new Range(f, t);
        }

        public override string ToString() => $"{From:yyyy-MM-dd} → {To:yyyy-MM-dd}";
    }

    /// The population behind a number. `Key` is what a drill-down link carries, so the
    /// list page rebuilds exactly the filter the card counted.
    public static ClaimFilter ScopeFilter(string scope, Range range, string? viewerId)
    {
        var f = new ClaimFilter { From = range.From, To = range.To };
        switch (scope)
        {
            // handler — always pinned to the viewer's own book (CR-5)
            case "mine_open": f.HandlerId = viewerId; f.OpenOnly = true; f.IgnoreRange = true; break;
            case "mine_due_today": f.HandlerId = viewerId; f.OpenOnly = true; f.DueToday = true; f.IgnoreRange = true; break;
            case "mine_overdue": f.HandlerId = viewerId; f.OpenOnly = true; f.SlaBreached = true; f.IgnoreRange = true; break;
            case "mine_stp_ready": f.HandlerId = viewerId; f.StpReady = true; f.IgnoreRange = true; break;
            case "mine_closed": f.HandlerId = viewerId; f.ClosedOnly = true; break;
            case "mine_stage_intake": f.HandlerId = viewerId; f.Stage = "intake"; f.IgnoreRange = true; break;
            case "mine_stage_extraction": f.HandlerId = viewerId; f.Stage = "extraction"; f.IgnoreRange = true; break;
            case "mine_stage_review": f.HandlerId = viewerId; f.Stage = "review"; f.IgnoreRange = true; break;
            case "mine_stage_decision": f.HandlerId = viewerId; f.Stage = "decision"; f.IgnoreRange = true; break;

            // team
            case "team_backlog": f.OpenOnly = true; f.IgnoreRange = true; break;
            case "team_sla_breach": f.OpenOnly = true; f.SlaBreached = true; f.IgnoreRange = true; break;
            case "team_decided": f.DecidedOnly = true; break;
            case "team_stp": f.DecidedOnly = true; f.StpOnly = true; break;
            case "team_siu": f.ToSiu = true; break;
            case "stage_intake": f.Stage = "intake"; f.IgnoreRange = true; break;
            case "stage_extraction": f.Stage = "extraction"; f.IgnoreRange = true; break;
            case "stage_review": f.Stage = "review"; f.IgnoreRange = true; break;
            case "stage_decision": f.Stage = "decision"; f.IgnoreRange = true; break;
            case "stage_settlement": f.Stage = "settlement"; f.IgnoreRange = true; break;
            case "age_0_1": f.OpenOnly = true; f.AgeMin = 0; f.AgeMax = 1; f.IgnoreRange = true; break;
            case "age_1_2": f.OpenOnly = true; f.AgeMin = 1; f.AgeMax = 2; f.IgnoreRange = true; break;
            case "age_2_3": f.OpenOnly = true; f.AgeMin = 2; f.AgeMax = 3; f.IgnoreRange = true; break;
            case "age_3_5": f.OpenOnly = true; f.AgeMin = 3; f.AgeMax = 5; f.IgnoreRange = true; break;
            case "age_5plus": f.OpenOnly = true; f.AgeMin = 5; f.IgnoreRange = true; break;

            // cfo — portfolio, no handler dimension (CR-5)
            case "all_incurred": break;
            case "all_outstanding": f.OpenOnly = true; f.IgnoreRange = true; break;

            default:
                if (scope.StartsWith("handler:", StringComparison.Ordinal))
                {
                    f.HandlerId = scope["handler:".Length..];
                    f.OpenOnly = true;
                    f.IgnoreRange = true;
                }
                break;
        }
        return f;
    }

    /// Scopes a viewer of this role is allowed to open (CR-5). A handler may only ever
    /// drill into their own book; the CFO may only ever drill into financial scopes.
    public static bool CanView(string role, string scope) => role switch
    {
        Auth.SuperAdminRole => true,
        "cfo" => scope.StartsWith("all_", StringComparison.Ordinal),
        "team_manager" or "senior_adjuster" => !scope.StartsWith("all_", StringComparison.Ordinal),
        _ => scope.StartsWith("mine_", StringComparison.Ordinal),
    };

    public static string Dashboard(string role) => role switch
    {
        "cfo" => "cfo",
        "team_manager" or "senior_adjuster" => "manager",
        Auth.SuperAdminRole => "platform",
        _ => "handler",
    };
}
