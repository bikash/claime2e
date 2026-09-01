using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

[Authorize(Policy = Auth.StaffPolicy)]
public class AnalyticsModel : PageModel
{
    public Db.BiHandler Handler { get; private set; } = new();
    public Db.BiManager Manager { get; private set; } = new();
    public Db.BiCfo Cfo { get; private set; } = new();
    public List<Db.HandlerLoad> Workload { get; private set; } = [];
    public List<Claim> LargestOpen { get; private set; } = [];
    public List<MonthStat> ClaimsByMonth { get; private set; } = [];
    public List<CostMonth> CostByMonth { get; private set; } = [];
    public StpSummary Stp { get; private set; } = new();
    public CitationHealth Citations { get; private set; } = new();

    public void OnGet()
    {
        var actorId = Auth.UserId(User);
        Handler = Db.HandlerKpis(actorId);
        Manager = Db.ManagerKpis();
        Cfo = Db.CfoKpis();
        Workload = Db.WorkloadByHandler();
        LargestOpen = Db.LargestOpenClaims(8);
        ClaimsByMonth = Db.ClaimStatsByMonth(6);
        CostByMonth = Db.UsageByMonth(6);
        Stp = Db.GetStpSummary();
        Citations = Db.GetCitationHealth();
    }
}
