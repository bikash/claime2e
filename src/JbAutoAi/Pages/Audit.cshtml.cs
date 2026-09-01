using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

[Authorize(Policy = Auth.StaffPolicy)]
public class AuditModel : PageModel
{
    public List<ActivityEntry> Entries { get; private set; } = [];
    public List<string> Kinds { get; private set; } = [];
    public string? Kind { get; private set; }

    public void OnGet(string? kind)
    {
        Kind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim();
        Entries = Db.ListAllActivity(200, Kind);
        Kinds = Db.ActivityKinds();
    }
}
