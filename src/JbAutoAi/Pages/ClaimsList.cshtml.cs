using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

/// The drill-down target for every dashboard KPI (CR-4).
///
/// The page never builds its own predicate: it hands the scope key back to
/// Metrics.ScopeFilter, so the list is exactly the population the card counted.
/// A scope outside the viewer's role is refused rather than silently widened (CR-5).
[Authorize(Policy = Auth.StaffPolicy)]
public class ClaimsListModel : PageModel
{
    public string Scope { get; private set; } = "";
    public string Role { get; private set; } = "";
    public Metrics.Range Period { get; private set; } = null!;
    public List<ClaimRow> Rows { get; private set; } = [];
    public bool Denied { get; private set; }
    public Dictionary<string, string> HandlerNames { get; private set; } = [];

    public IActionResult OnGet(string? scope, string? from, string? to)
    {
        Scope = scope ?? "team_backlog";
        Role = Auth.UserRole(User);
        Period = Metrics.Range.Parse(from, to, DateOnly.FromDateTime(DateTime.UtcNow));

        if (!Metrics.CanView(Role, Scope))
        {
            Denied = true;
            return Page();
        }

        Rows = Db.QueryClaims(Metrics.ScopeFilter(Scope, Period, Auth.UserId(User)));
        HandlerNames = Db.ListHandlers().ToDictionary(h => h.Id, h => h.Name);
        return Page();
    }
}
