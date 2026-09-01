using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

[Authorize(Policy = Auth.SuperAdminPolicy)]
public class WorkflowEditModel : PageModel
{
    public Workflow Workflow { get; private set; } = new();
    public List<WorkflowStep> Steps { get; private set; } = [];
    public List<WorkflowRun> Runs { get; private set; } = [];
    public List<Claim> RecentClaims { get; private set; } = [];
    public string? Notice { get; private set; }
    public string? RunError { get; private set; }

    public IActionResult OnGet(string id, string? notice, string? err)
    {
        var wf = Db.GetWorkflow(id);
        if (wf is null) return NotFound();
        Workflow = wf;
        Steps = Db.ListSteps(id);
        Runs = Db.ListRuns(id, 20);
        RecentClaims = Db.ListClaims(20);
        Notice = notice;
        RunError = err;
        return Page();
    }

    public IActionResult OnPostSave(string id, string name, string triggerKind, bool active)
    {
        var wf = Db.GetWorkflow(id);
        if (wf is null) return NotFound();
        wf.Name = string.IsNullOrWhiteSpace(name) ? wf.Name : name.Trim();
        wf.TriggerKind = string.IsNullOrWhiteSpace(triggerKind) ? wf.TriggerKind : triggerKind;
        wf.Active = active;
        Db.UpsertWorkflow(wf);
        return Redirect($"/workflows/{id}?notice=saved");
    }

    /// Steps ship as parallel form arrays: kind[] and config[]. Empty rows are
    /// dropped so the operator can just leave a slot blank to remove a step.
    public IActionResult OnPostSaveSteps(string id)
    {
        var kinds = Request.Form["kind"];
        var cfgs = Request.Form["config"];
        var steps = new List<WorkflowStep>();
        for (int i = 0; i < kinds.Count; i++)
        {
            var k = kinds[i];
            if (string.IsNullOrWhiteSpace(k)) continue;
            steps.Add(new WorkflowStep
            {
                WorkflowId = id,
                Ordinal = steps.Count + 1,
                Kind = k!,
                Config = i < cfgs.Count && !string.IsNullOrWhiteSpace(cfgs[i]) ? cfgs[i] : null,
            });
        }
        Db.ReplaceSteps(id, steps);
        return Redirect($"/workflows/{id}?notice=steps");
    }

    public async Task<IActionResult> OnPostRunAsync(string id, string? claimId)
    {
        var (status, err, _) = await WorkflowRunner.RunAsync(
            id, string.IsNullOrWhiteSpace(claimId) ? null : claimId, "manual",
            Auth.UserId(User));
        return Redirect($"/workflows/{id}?notice=ran:{status}" +
                        (err is null ? "" : $"&err={Uri.EscapeDataString(err)}"));
    }
}
