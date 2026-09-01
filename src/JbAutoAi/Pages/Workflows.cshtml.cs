using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

[Authorize(Policy = Auth.SuperAdminPolicy)]
public class WorkflowsModel : PageModel
{
    public List<Workflow> Items { get; private set; } = [];
    public List<WorkflowRun> RecentRuns { get; private set; } = [];
    public string? Notice { get; private set; }

    [BindProperty] public string NewName { get; set; } = "";
    [BindProperty] public string NewTrigger { get; set; } = "manual";

    public void OnGet(string? notice)
    {
        Items = Db.ListWorkflows();
        RecentRuns = Db.ListRuns(null, 15);
        Notice = notice;
    }

    public IActionResult OnPostCreate()
    {
        if (string.IsNullOrWhiteSpace(NewName))
            return Redirect("/workflows?notice=missing");
        var slug = Slug(NewName);
        Db.UpsertWorkflow(new Workflow
        {
            Id = slug,
            Name = NewName.Trim(),
            TriggerKind = NewTrigger,
            Active = true,
        }, Auth.UserId(User));
        return Redirect($"/workflows/{slug}");
    }

    public IActionResult OnPostDelete(string id)
    {
        Db.DeleteWorkflow(id);
        return Redirect("/workflows?notice=deleted");
    }

    static string Slug(string s)
    {
        var chars = s.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return string.IsNullOrEmpty(slug) ? $"wf-{DateTime.UtcNow.Ticks:x}" : slug;
    }
}
