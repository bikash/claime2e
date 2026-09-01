using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

/// The visual flow behind one agent: components on a canvas, wired together.
///
/// Designing agents is the super admin's job (same rule as the create form in
/// Agents.cshtml), so the whole page sits behind that policy rather than hiding
/// the save button from everyone else.
[Authorize(Policy = Auth.SuperAdminPolicy)]
public class AgentFlowModel : PageModel
{
    public AgentDef Agent { get; private set; } = new();
    public string? Notice { get; private set; }

    [BindProperty] public string Flow { get; set; } = "";

    public IActionResult OnGet(string aid, string? notice)
    {
        if (Db.GetAgent(aid) is not { } agent) return NotFound();
        Agent = agent;
        Notice = notice;
        return Page();
    }

    public IActionResult OnPost(string aid)
    {
        if (Db.GetAgent(aid) is null) return NotFound();

        // The canvas posts its own JSON back. Parse it before it reaches the column so a
        // broken client cannot write something the reader will choke on.
        FlowGraph? graph;
        try { graph = Json.Parse<FlowGraph>(Flow); }
        catch (System.Text.Json.JsonException) { return Redirect($"/agents/{aid}/flow"); }
        if (graph is null) return Redirect($"/agents/{aid}/flow");

        Db.SetAgentFlow(aid, Json.Str(graph));
        Db.AddActivity(null, "agent_flow", Auth.UserId(User),
            $"Flow saved for {Db.GetAgent(aid)!.Name}",
            Json.Str(new { agent_id = aid, nodes = graph.Nodes.Count, edges = graph.Edges.Count }));
        return Redirect($"/agents/{aid}/flow?notice=saved");
    }

    public class FlowGraph
    {
        public List<FlowNode> Nodes { get; set; } = [];
        public List<List<string>> Edges { get; set; } = [];
    }

    public class FlowNode
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Label { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }

        // What the component consumes and produces, plus its instructions — free text,
        // set in the canvas inspector. Design-time documentation, carried whole in the
        // flow JSON.
        public string? Input { get; set; }
        public string? Output { get; set; }
        public string? Prompt { get; set; }
    }
}
