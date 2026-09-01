using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

[Authorize(Policy = Auth.StaffPolicy)]
public class ClaimModel : PageModel
{
    public Claim Claim { get; private set; } = new();
    public List<Document> Documents { get; private set; } = [];
    public List<Handler> Handlers { get; private set; } = [];
    public List<EmailTemplate> Templates { get; private set; } = [];
    public List<ActivityEntry> Activity { get; private set; } = [];
    public List<LegalHit> Citations { get; private set; } = [];
    public Handler? Assigned { get; private set; }
    public Handler Actor { get; private set; } = new();

    public List<Rules.Reason> Reasons { get; private set; } = [];
    public List<Rules.Signal> Signals { get; private set; } = [];
    public JsonObject Assessment { get; private set; } = [];
    public JsonObject Liability { get; private set; } = [];
    public List<string> DamageAreas { get; private set; } = [];

    public IActionResult OnGet(string cid)
    {
        var claim = Db.GetClaim(cid);
        if (claim is null) return NotFound();
        Claim = claim;

        Documents = Db.GetDocuments(cid);
        Handlers = Db.ListHandlers();
        Templates = Db.ListEmailTemplates();
        Activity = Db.ListActivity(cid);
        Citations = Db.GetClaimCitations(cid);
        Assigned = Db.GetHandler(claim.AssignedHandlerId);
        Actor = Db.GetHandler(Auth.UserId(User)) ?? new Handler();

        Reasons = Json.Parse<List<Rules.Reason>>(claim.DecisionReasons) ?? [];
        Signals = Json.Parse<List<Rules.Signal>>(claim.FraudSignals) ?? [];
        DamageAreas = Json.Parse<List<string>>(claim.DamageCategories) ?? [];
        Assessment = JsonNode.Parse(claim.Assessment ?? "{}") as JsonObject ?? [];
        Liability = Assessment["liability"] as JsonObject ?? [];

        // Rendered inside a <script> block, so it needs the HTML-safe encoder.
        ViewData["ClaimCtx"] = Json.ForScriptTag(new
        {
            id = claim.Id,
            claim_number = claim.ClaimNumber,
            policyholder_name = claim.PolicyholderName,
        });
        return Page();
    }

    public static string? Str(JsonObject o, string key) =>
        o[key]?.ToString() is { Length: > 0 } s ? s : null;

    public static int Int(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;

    public static bool Bool(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
}
