using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JbAutoAi;

/// v1 workflow runner. Loads ordered steps for a workflow, dispatches per
/// step-kind to an inline handler, and journals each run to workflow_run.
///
/// ponytail: single-file dispatcher, no retries, no branching, no parallel
/// steps. Upgrade path — split step handlers into files + add a Polly retry
/// policy when we have flows that actually need it.
public static class WorkflowRunner
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public sealed class Ctx
    {
        public string? ClaimId { get; init; }
        public string? TriggerRef { get; init; }
        public Dictionary<string, JsonNode?> Bag { get; } = new();
        public List<string> Log { get; } = new();
    }

    public static async Task<(string Status, string? Error, string ContextJson)> RunAsync(
        string workflowId, string? claimId, string? triggerRef, string? actorHandlerId = null)
    {
        var wf = Db.GetWorkflow(workflowId) ?? throw new InvalidOperationException("workflow not found");
        if (!wf.Active) return ("error", "workflow inactive", "{}");
        var steps = Db.ListSteps(workflowId);
        var runId = Db.InsertRun(workflowId, claimId, triggerRef);
        var ctx = new Ctx { ClaimId = claimId, TriggerRef = triggerRef };

        try
        {
            foreach (var s in steps)
            {
                var cfg = ParseCfg(s.Config);
                ctx.Log.Add($"→ step {s.Ordinal} {s.Kind}");
                await DispatchAsync(s.Kind, cfg, ctx);
            }
            var ctxJson = SerializeCtx(ctx);
            Db.FinishRun(runId, "ok", ctxJson, null);
            if (claimId is not null)
                Db.AddActivity(claimId, "workflow_run",
                    actorHandlerId is { Length: > 0 } ? actorHandlerId : null,
                    $"Workflow '{wf.Name}' completed ({steps.Count} step(s)).",
                    Json.Str(new { workflow_id = workflowId, run_id = runId, status = "ok" }));
            return ("ok", null, ctxJson);
        }
        catch (Exception ex)
        {
            var ctxJson = SerializeCtx(ctx);
            Db.FinishRun(runId, "error", ctxJson, ex.Message);
            if (claimId is not null)
                Db.AddActivity(claimId, "workflow_run",
                    actorHandlerId is { Length: > 0 } ? actorHandlerId : null,
                    $"Workflow '{wf.Name}' failed: {ex.Message}",
                    Json.Str(new { workflow_id = workflowId, run_id = runId, status = "error" }));
            return ("error", ex.Message, ctxJson);
        }
    }

    static async Task DispatchAsync(string kind, JsonObject cfg, Ctx ctx)
    {
        switch (kind)
        {
            case "classify":     await ClassifyAsync(cfg, ctx); break;
            case "extract":      await ExtractAsync(cfg, ctx); break;
            case "email":        await EmailAsync(cfg, ctx); break;
            case "decision":     await DecisionAsync(cfg, ctx); break;
            case "crm_push":     await HttpAsync(cfg, ctx, "crm_push"); break;
            case "webhook_call": await HttpAsync(cfg, ctx, "webhook_call"); break;
            case "agent":        await AgentAsync(cfg, ctx); break;
            case "note":         NoteStep(cfg, ctx); break;
            default: throw new InvalidOperationException($"unknown step kind: {kind}");
        }
    }

    // --- step handlers ----------------------------------------------------------

    static async Task ClassifyAsync(JsonObject cfg, Ctx ctx)
    {
        var claim = LoadClaim(ctx);
        var prompt = cfg["prompt"]?.ToString() ??
            "Classify this claim into one of: minor_damage, major_damage, total_loss, injury, fraud_suspect.";
        var field = cfg["field"]?.ToString() ?? "classification";
        var user = $"Claim {claim.ClaimNumber}. Description: {claim.Description}. Amount: €{claim.EstimatedAmountEur:F0}. Injuries: {claim.Injuries}.\n\nTask: {prompt}\nRespond with the single label, nothing else.";
        var text = await Llm.RunPromptAsync(
            "You are a claim classifier. Reply with one lowercase snake_case label from the list.",
            user, "workflow.classify", ctx.ClaimId);
        ctx.Bag[field] = JsonValue.Create(text.Trim());
        ctx.Log.Add($"  {field} = {text.Trim()}");
    }

    static async Task ExtractAsync(JsonObject cfg, Ctx ctx)
    {
        var claim = LoadClaim(ctx);
        var fields = cfg["fields"]?.AsArray()?.Select(n => n?.ToString() ?? "").Where(s => s.Length > 0).ToList()
                     ?? new List<string> { "estimated_amount_eur", "loss_date", "location" };
        var prompt = cfg["prompt"]?.ToString() ?? "Extract the requested fields as JSON.";
        var user = $"{prompt}\n\nFields: {string.Join(", ", fields)}\n\nClaim text:\n{claim.Description}\n\nReturn one JSON object with those fields.";
        var node = await Llm.RunPromptJsonAsync(
            "You extract structured data from claim descriptions. Reply with a JSON object only.",
            user, "workflow.extract", ctx.ClaimId);
        ctx.Bag["extract"] = node.DeepClone();
        ctx.Log.Add($"  extract → {string.Join(",", fields)}");
    }

    static async Task EmailAsync(JsonObject cfg, Ctx ctx)
    {
        var claim = LoadClaim(ctx);
        var templateId = cfg["templateId"]?.ToString() ?? throw new InvalidOperationException("email step needs templateId");
        var template = Db.ListEmailTemplates().FirstOrDefault(t => t.Id == templateId)
            ?? throw new InvalidOperationException($"template '{templateId}' not found");
        // Draft-only in v1. SMTP send is one wire away when creds are provided.
        var to  = cfg["to"]?.ToString() ?? claim.PolicyholderName;
        var body = template.Body.Replace("{claim_number}", claim.ClaimNumber)
                                .Replace("{policyholder}", claim.PolicyholderName);
        Db.AddActivity(ctx.ClaimId!, "email_saved", null,
            $"Workflow drafted email via template '{template.Name}'.",
            Json.Str(new { to, subject = template.Subject, body, template_id = templateId }));
        ctx.Log.Add($"  email drafted to {to}");
        await Task.CompletedTask;
    }

    static async Task DecisionAsync(JsonObject cfg, Ctx ctx)
    {
        if (ctx.ClaimId is null) throw new InvalidOperationException("decision step needs a claim");
        var status = await Pipeline.AnalyseAsync(ctx.ClaimId);
        ctx.Bag["decision"] = JsonValue.Create(status);
        ctx.Log.Add($"  decision = {status}");
    }

    static async Task HttpAsync(JsonObject cfg, Ctx ctx, string opName)
    {
        var url = cfg["url"]?.ToString() ?? throw new InvalidOperationException($"{opName} step needs url");
        var method = (cfg["method"]?.ToString() ?? "POST").ToUpperInvariant();
        var payload = cfg["payload"] ?? BuildDefaultPayload(ctx);
        var req = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        // Headers: value "env:NAME" is dereferenced from process env. Keeps
        // secrets out of the DB while still being step-configurable.
        if (cfg["headers"] is JsonObject headers)
            foreach (var kv in headers)
            {
                var v = kv.Value?.ToString() ?? "";
                if (v.StartsWith("env:")) v = Environment.GetEnvironmentVariable(v[4..]) ?? "";
                req.Headers.TryAddWithoutValidation(kv.Key, v);
            }
        using var resp = await Http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        ctx.Bag[opName] = new JsonObject
        {
            ["status"] = (int)resp.StatusCode,
            ["body"]   = body.Length > 2000 ? body[..2000] : body,
        };
        ctx.Log.Add($"  {opName} {method} {url} → {(int)resp.StatusCode}");
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"{opName} HTTP {(int)resp.StatusCode}: {body[..Math.Min(240, body.Length)]}");
    }

    /// Runs a studio agent (Pages/Agents.cshtml) as a workflow step. The agent's own
    /// prompt is the system prompt; the claim plus every earlier step's output is the
    /// user message, so chained agents build on each other through ctx.Bag.
    static async Task AgentAsync(JsonObject cfg, Ctx ctx)
    {
        var agentId = cfg["agentId"]?.ToString()
            ?? throw new InvalidOperationException("agent step needs agentId — pick an agent in the builder inspector");
        var agent = Db.GetAgent(agentId) ?? throw new InvalidOperationException($"agent '{agentId}' not found");
        if (!agent.Active) throw new InvalidOperationException($"agent '{agent.Name}' is deactivated");

        var claim = LoadClaim(ctx);
        var sb = new StringBuilder();
        sb.AppendLine($"Claim {claim.ClaimNumber} — {claim.PolicyholderName}, plate {claim.LicensePlate}, " +
                      $"amount €{claim.EstimatedAmountEur:F0}, injuries: {claim.Injuries}.");
        sb.AppendLine($"Description: {claim.Description}");
        if (ctx.Bag.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Results from earlier workflow steps:");
            foreach (var kv in ctx.Bag)
                sb.AppendLine($"- {kv.Key}: {kv.Value?.ToJsonString()}");
        }

        // A per-step prompt from the builder inspector adds to the agent's own prompt,
        // so one agent can be tuned per workflow without editing the agent itself.
        var system = agent.Prompt;
        if (cfg["prompt"]?.ToString() is { Length: > 0 } extra)
            system += "\n\nStep-specific instructions:\n" + extra;

        var text = await Llm.RunPromptAsync(system, sb.ToString(), "workflow.agent", ctx.ClaimId);
        ctx.Bag["agent_" + agentId] = JsonValue.Create(text);
        if (ctx.ClaimId is not null)
            Db.AddActivity(ctx.ClaimId, "agent_output", null,
                $"Agent '{agent.Name}' ran in workflow.",
                Json.Str(new { agent_id = agentId, output = text }));
        ctx.Log.Add($"  agent {agentId} ok ({text.Length} chars)");
    }

    static void NoteStep(JsonObject cfg, Ctx ctx)
    {
        var body = cfg["body"]?.ToString() ?? "workflow note";
        if (ctx.ClaimId is not null)
            Db.AddActivity(ctx.ClaimId, "note", null, "Workflow: " + body);
        ctx.Log.Add($"  note added");
    }

    // --- helpers ----------------------------------------------------------------

    static Claim LoadClaim(Ctx ctx)
    {
        if (ctx.ClaimId is null) throw new InvalidOperationException("step needs a claim");
        return Db.GetClaim(ctx.ClaimId) ?? throw new InvalidOperationException("claim not found");
    }

    static JsonObject ParseCfg(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try { return (JsonNode.Parse(json) as JsonObject) ?? new JsonObject(); }
        catch (JsonException) { return new JsonObject(); }
    }

    static JsonObject BuildDefaultPayload(Ctx ctx)
    {
        var claim = ctx.ClaimId is null ? null : Db.GetClaim(ctx.ClaimId);
        var p = new JsonObject
        {
            ["claim_id"]       = ctx.ClaimId,
            ["claim_number"]   = claim?.ClaimNumber,
            ["policyholder"]   = claim?.PolicyholderName,
            ["license_plate"]  = claim?.LicensePlate,
            ["estimated_eur"]  = claim?.EstimatedAmountEur,
            ["decision"]       = claim?.DecisionOutcome,
        };
        foreach (var kv in ctx.Bag)
            p[kv.Key] = kv.Value?.DeepClone();
        return p;
    }

    static string SerializeCtx(Ctx ctx)
    {
        var obj = new JsonObject
        {
            ["claim_id"] = ctx.ClaimId,
            ["trigger_ref"] = ctx.TriggerRef,
            ["log"] = new JsonArray(ctx.Log.Select(l => (JsonNode)JsonValue.Create(l)!).ToArray()),
            ["bag"] = new JsonObject(),
        };
        foreach (var kv in ctx.Bag)
            ((JsonObject)obj["bag"]!)[kv.Key] = kv.Value?.DeepClone();
        return obj.ToJsonString();
    }
}
