using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

/// Agent studio. Every staff member sees the agents released to them and can
/// try them on the test bench; designing agents and granting access — to the
/// whole company or to a single handler — is the super admin's job.
[Authorize(Policy = Auth.StaffPolicy)]
public class AgentsModel : PageModel
{
    public Handler Me { get; private set; } = new();
    public bool IsAdmin => Me.Role == Auth.SuperAdminRole;

    public List<AgentDef> Agents { get; private set; } = [];
    public List<AgentGrant> Grants { get; private set; } = [];
    public List<Handler> Handlers { get; private set; } = [];
    public string? Notice { get; private set; }

    public string? TestAgentId { get; private set; }
    public string? TestScenario { get; private set; }
    public string? TestAnswer { get; private set; }

    // create form
    [BindProperty] public string NewName { get; set; } = "";
    [BindProperty] public string NewTemplate { get; set; } = "summariser";
    [BindProperty] public string NewLang { get; set; } = "both";
    [BindProperty] public string NewTone { get; set; } = "concise";
    [BindProperty] public string NewTrigger { get; set; } = "manual";
    [BindProperty] public string NewAutonomy { get; set; } = "suggest";
    [BindProperty] public string NewPrompt { get; set; } = "";
    [BindProperty] public List<string> NewTools { get; set; } = [];

    public static readonly string[] ToolChoices =
        ["Policy lookup", "RDW vehicle registry", "CIS fraud database", "Repair network Schadegarant", "Email & letters"];
    public static readonly string[] Templates = ["summariser", "intake", "fraud", "comms", "reserve"];

    void Load(string? notice = null)
    {
        Me = Db.GetHandler(Auth.UserId(User)) ?? new Handler();
        Agents = IsAdmin ? Db.ListAgents() : Db.ListAgentsFor(Me.Id);
        Grants = Db.ListAgentGrants();
        Handlers = Db.ListHandlers();
        Notice = notice;
    }

    public void OnGet(string? notice) => Load(notice);

    public IActionResult OnPostCreate()
    {
        Load();
        if (!IsAdmin) return Forbid();
        if (string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewPrompt))
            return Redirect("/agents?notice=missing");

        var id = "ag_" + Guid.NewGuid().ToString("N")[..8];
        Db.CreateAgent(new AgentDef
        {
            Id = id, Name = NewName.Trim(), Template = NewTemplate, Lang = NewLang,
            Tone = NewTone, TriggerKind = NewTrigger, Autonomy = NewAutonomy,
            Prompt = NewPrompt.Trim(), CreatedBy = Me.Id,
            Tools = System.Text.Json.JsonSerializer.Serialize(
                NewTools.Where(t => ToolChoices.Contains(t)).ToList()),
        });
        Db.AddActivity(null, "agent_created", Me.Id, $"Agent '{NewName.Trim()}' created ({NewTemplate})");
        return Redirect("/agents?notice=created");
    }

    public IActionResult OnPostToggle(string id)
    {
        Load();
        if (!IsAdmin) return Forbid();
        var agent = Db.GetAgent(id);
        if (agent is null) return Redirect("/agents");
        Db.SetAgentActive(id, !agent.Active);
        Db.AddActivity(null, "agent_toggled", Me.Id,
            $"Agent '{agent.Name}' {(agent.Active ? "paused" : "activated")}");
        return Redirect("/agents");
    }

    public IActionResult OnPostGrant(string agentId, string? handlerId)
    {
        Load();
        if (!IsAdmin) return Forbid();
        var agent = Db.GetAgent(agentId);
        if (agent is null) return Redirect("/agents");
        handlerId = string.IsNullOrEmpty(handlerId) ? null : handlerId;
        Db.AddAgentGrant(agentId, handlerId, Me.Id);
        var who = handlerId is null ? "entire company" : Db.GetHandler(handlerId)?.Name ?? handlerId;
        Db.AddActivity(null, "agent_granted", Me.Id, $"Agent '{agent.Name}' access granted to {who}");
        return Redirect("/agents?notice=granted");
    }

    public IActionResult OnPostRevoke(long grantId)
    {
        Load();
        if (!IsAdmin) return Forbid();
        var grant = Grants.FirstOrDefault(g => g.Id == grantId);
        Db.RemoveAgentGrant(grantId);
        if (grant is not null)
        {
            var agent = Db.GetAgent(grant.AgentId);
            Db.AddActivity(null, "agent_revoked", Me.Id,
                $"Agent '{agent?.Name ?? grant.AgentId}' access revoked for {grant.HandlerName ?? "entire company"}");
        }
        return Redirect("/agents?notice=revoked");
    }

    public async Task<IActionResult> OnPostTestAsync(string agentId, string scenario)
    {
        Load();
        var agent = Db.GetAgent(agentId);
        // Paused is a kill switch: hide it from the UI *and* refuse hand-crafted posts.
        if (agent is null || !agent.Active || string.IsNullOrWhiteSpace(scenario)) return Page();
        if (!IsAdmin && !Db.HasAgentAccess(agentId, Me.Id)) return Forbid();

        var lang = agent.Lang switch { "nl" => "Nederlands", "en" => "English", _ => I18n.PromptLanguage };
        var system = $"""
            {agent.Prompt}
            Tone: {agent.Tone}. Respond in {lang}.
            Available tools (simulated in this environment — say when you would use one): {string.Join(", ", agent.ToolList)}.
            """;
        TestAgentId = agentId;
        TestScenario = scenario.Trim();
        TestAnswer = await Llm.RunPromptAsync(system, TestScenario, "agent_bench");
        Db.AddActivity(null, "agent_bench", Me.Id, $"Test bench: '{agent.Name}' ran a scenario");
        return Page();
    }
}
