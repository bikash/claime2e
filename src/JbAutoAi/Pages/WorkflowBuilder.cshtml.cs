using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

/// Visual builder for one workflow.
///
/// The canvas is the editor; workflow_step stays the execution contract. On save the
/// graph is stored whole in workflow.config and then *flattened* into ordered steps by
/// a topological walk, so WorkflowRunner keeps running exactly what it always ran.
///
/// Modules the runner has no handler for (triggers, connectors, AI modules, human
/// gates) are written as `note` steps carrying their module type, rather than dropped
/// on the floor or faked into an executable kind that would misfire at runtime.
[Authorize(Policy = Auth.SuperAdminPolicy)]
public class WorkflowBuilderModel : PageModel
{
    public Workflow Workflow { get; private set; } = new();
    public string Graph { get; private set; } = """{"nodes":[],"conns":[]}""";
    public string? Notice { get; private set; }

    [BindProperty] public string GraphJson { get; set; } = "";

    /// Module type → the runner kind that actually executes it. Anything absent is
    /// design-time only and is recorded, not executed.
    public static readonly Dictionary<string, string> Executable = new()
    {
        ["classify"] = "classify",
        ["extract"] = "extract",
        ["correspondence"] = "email",
        ["crm_hubspot"] = "crm_push",
        ["webhook"] = "webhook_call",
        ["mcp"] = "webhook_call",
        ["decision"] = "decision",
        ["coverage"] = "decision",
        ["agent"] = "agent",
    };

    /// Active studio agents, offered as options on the `agent` module's picker.
    public string[] AgentChoices { get; private set; } = [];

    public IActionResult OnGet(string id, string? notice)
    {
        if (Db.GetWorkflow(id) is not { } w) return NotFound();
        Workflow = w;
        Notice = notice;
        AgentChoices = Db.ListAgents().Where(a => a.Active).Select(a => a.Id).ToArray();

        if (w.Config is { Length: > 0 } cfg && JsonNode.Parse(cfg) is JsonObject o && o["nodes"] is not null)
            Graph = cfg;

        return Page();
    }

    public IActionResult OnPost(string id)
    {
        if (Db.GetWorkflow(id) is not { } w) return NotFound();

        JsonObject? graph;
        try { graph = JsonNode.Parse(GraphJson) as JsonObject; }
        catch (System.Text.Json.JsonException) { return Redirect($"/workflows/{id}/builder?notice=bad"); }
        if (graph?["nodes"] is not JsonArray nodes) return Redirect($"/workflows/{id}/builder?notice=bad");

        var conns = graph["conns"] as JsonArray ?? [];
        w.Config = graph.ToJsonString();
        Db.UpsertWorkflow(w, Auth.UserId(User));
        Db.ReplaceSteps(id, Flatten(nodes, conns));

        Db.AddActivity(null, "workflow_saved", Auth.UserId(User),
            $"Workflow builder saved: {w.Name}",
            Json.Str(new { workflow_id = id, nodes = nodes.Count, conns = conns.Count }));

        return Redirect($"/workflows/{id}/builder?notice=saved");
    }

    /// Kahn's algorithm over the wired graph. Cycles cannot deadlock the save: whatever
    /// is still unvisited is appended in canvas order so no module is lost.
    static List<WorkflowStep> Flatten(JsonArray nodes, JsonArray conns)
    {
        var byId = nodes.OfType<JsonObject>()
                        .Where(n => n["id"]?.ToString() is { Length: > 0 })
                        .ToDictionary(n => n["id"]!.ToString(), n => n);

        var incoming = byId.Keys.ToDictionary(k => k, _ => 0);
        var outgoing = byId.Keys.ToDictionary(k => k, _ => new List<string>());
        foreach (var c in conns.OfType<JsonObject>())
        {
            var from = c["from"]?.ToString();
            var to = c["to"]?.ToString();
            if (from is null || to is null || !byId.ContainsKey(from) || !byId.ContainsKey(to)) continue;
            outgoing[from].Add(to);
            incoming[to]++;
        }

        var queue = new Queue<string>(incoming.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var order = new List<string>();
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            order.Add(id);
            foreach (var next in outgoing[id])
                if (--incoming[next] == 0) queue.Enqueue(next);
        }
        foreach (var id in byId.Keys) if (!order.Contains(id)) order.Add(id);

        var steps = new List<WorkflowStep>();
        foreach (var id in order)
        {
            var n = byId[id];
            var module = n["type"]?.ToString() ?? "note";
            var cfg = n["cfg"] as JsonObject ?? [];
            cfg["module"] = module;
            if (n["title"]?.ToString() is { Length: > 0 } title) cfg["label"] = title;

            steps.Add(new WorkflowStep
            {
                Kind = Executable.GetValueOrDefault(module, "note"),
                Config = cfg.ToJsonString(),
            });
        }
        return steps;
    }
}
