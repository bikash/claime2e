using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

/// The role dashboards (spec §3–§5). One page, four views: Handler "My work",
/// Team Manager "Team overview", CFO "Financials", and the existing platform view
/// for the super admin.
///
/// Every KPI here resolves through Metrics/Db — never an ad-hoc query in the view
/// (CR-1) — and every card carries the scope key its drill-down link replays (CR-4).
[Authorize(Policy = Auth.StaffPolicy)]
public class IndexModel : PageModel
{
    public const int Size = 15;

    public string Role { get; private set; } = "";
    public string View { get; private set; } = "handler";
    public string? MyId { get; private set; }
    public bool IsSuperAdmin => View == "platform";
    public Metrics.Range Period { get; private set; } = null!;
    public DateTime LoadedAt { get; private set; }          // CR-3 freshness stamp

    // --- handler ("My work") ---
    public int OpenCases { get; private set; }
    public int OpenCasesPrior { get; private set; }
    public int DueToday { get; private set; }
    public int Overdue { get; private set; }
    public int StpReady { get; private set; }
    public List<ClaimRow> StpReadyClaims { get; private set; } = [];
    public Dictionary<string, int> MyStages { get; private set; } = [];
    public List<ClaimRow> MyAging { get; private set; } = [];
    public List<Db.DayCount> ClosedPerDay { get; private set; } = [];
    public List<ActivityEntry> MyActivity { get; private set; } = [];

    // --- manager ("Team overview") ---
    public int Decided { get; private set; }
    public int Stp { get; private set; }
    public double StpRate => Decided == 0 ? 0 : (double)Stp / Decided;
    public double StpRatePrior { get; private set; }
    public int Backlog { get; private set; }
    public int BacklogPrior { get; private set; }
    public int SlaBreaches { get; private set; }
    public int ToSiu { get; private set; }
    public (double? P50, double? P95, int N) Cycle { get; private set; }
    public List<Db.WeekRate> StpWeeks { get; private set; } = [];
    public List<Db.Workload> Workload { get; private set; } = [];
    public Dictionary<string, int> Stages { get; private set; } = [];
    public List<(string Key, int Count)> AgeBuckets { get; private set; } = [];

    // --- cfo ("Financials") ---
    public double Incurred { get; private set; }
    public double IncurredPrior { get; private set; }
    public double Outstanding { get; private set; }
    public List<Db.MonthCost> CostMonths { get; private set; } = [];

    // --- platform (super admin, unchanged) ---
    public List<Claim> Claims { get; private set; } = [];
    public List<Handler> Handlers { get; private set; } = [];
    public string Query { get; private set; } = "";
    public int CurrentPage { get; private set; } = 1;
    public int TotalPages { get; private set; } = 1;
    public int TotalClaims { get; private set; }
    public StpSummary StpSum { get; private set; } = new();
    public UsageTotals Usage { get; private set; } = new();
    public CitationHealth Citations { get; private set; } = new();
    public List<DayStat> ClaimsByDay { get; private set; } = [];
    public List<CostDay> CostByDay { get; private set; } = [];
    public List<MonthStat> ClaimsByMonth { get; private set; } = [];
    public List<CostMonth> CostByMonth { get; private set; } = [];

    public void OnGet(string? q, int page = 1, string? from = null, string? to = null)
    {
        Query = (q ?? "").Trim();
        CurrentPage = page < 1 ? 1 : page;
        Role = Auth.UserRole(User);
        MyId = Auth.UserId(User);
        View = Metrics.Dashboard(Role);
        Period = Metrics.Range.Parse(from, to, DateOnly.FromDateTime(DateTime.UtcNow));
        LoadedAt = DateTime.UtcNow;

        switch (View)
        {
            case "handler": LoadHandler(); break;
            case "manager": LoadManager(); break;
            case "cfo": LoadCfo(); break;
            default: LoadPlatform(); break;
        }
    }

    int Count(string scope) => Db.CountClaims(Metrics.ScopeFilter(scope, Period, MyId));
    int CountPrior(string scope) => Db.CountClaims(Metrics.ScopeFilter(scope, Period.Prior(), MyId));

    void LoadHandler()
    {
        OpenCases = Count("mine_open");
        OpenCasesPrior = CountPrior("mine_open");
        DueToday = Count("mine_due_today");
        Overdue = Count("mine_overdue");
        StpReady = Count("mine_stp_ready");
        StpReadyClaims = Db.QueryClaims(Metrics.ScopeFilter("mine_stp_ready", Period, MyId), 5);

        foreach (var stage in new[] { "intake", "extraction", "review", "decision" })
            MyStages[stage] = Count("mine_stage_" + stage);

        // HD-1's aging note: anything past the SLA threshold, oldest first.
        MyAging = Db.QueryClaims(Metrics.ScopeFilter("mine_overdue", Period, MyId), 5);
        ClosedPerDay = Db.ClosedPerDay(Period, MyId);
        MyActivity = Db.RecentByActor(MyId, 8);
    }

    void LoadManager()
    {
        Decided = Count("team_decided");
        Stp = Count("team_stp");
        var priorDecided = CountPrior("team_decided");
        StpRatePrior = priorDecided == 0 ? 0 : (double)CountPrior("team_stp") / priorDecided;
        Backlog = Count("team_backlog");
        BacklogPrior = CountPrior("team_backlog");
        SlaBreaches = Count("team_sla_breach");
        ToSiu = Count("team_siu");
        Cycle = Db.CycleTime(Period);

        StpWeeks = Db.StpByWeek(8);
        Workload = Db.WorkloadForRebalance();
        foreach (var stage in new[] { "intake", "extraction", "review", "decision", "settlement" })
            Stages[stage] = Count("stage_" + stage);
        AgeBuckets = [.. new[] { "age_0_1", "age_1_2", "age_2_3", "age_3_5", "age_5plus" }
            .Select(k => (k, Count(k)))];
    }

    void LoadCfo()
    {
        Incurred = Db.SumAmount(Metrics.ScopeFilter("all_incurred", Period, null));
        IncurredPrior = Db.SumAmount(Metrics.ScopeFilter("all_incurred", Period.Prior(), null));
        Outstanding = Db.SumAmount(Metrics.ScopeFilter("all_outstanding", Period, null));
        CostMonths = Db.CostByMonth(Period);
    }

    void LoadPlatform()
    {
        if (Query.Length > 0)
        {
            Claims = Db.SearchClaims(Query, 100);
            TotalClaims = Claims.Count;
            TotalPages = 1;
        }
        else
        {
            var (items, total) = Db.ListClaimsPaged(CurrentPage, Size);
            Claims = items;
            TotalClaims = total;
            TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)Size));
        }

        Handlers = Db.ListHandlers();
        StpSum = Db.GetStpSummary();
        Usage = Db.GetUsageTotals();
        Citations = Db.GetCitationHealth();
        ClaimsByDay = Db.ClaimStatsByDay(14);
        CostByDay = Db.UsageByDay(14);
        ClaimsByMonth = Db.ClaimStatsByMonth(6);
        CostByMonth = Db.UsageByMonth(6);
    }
}
