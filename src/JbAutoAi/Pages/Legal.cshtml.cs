using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

/// FR-11 legal lookup: the same hybrid retrieval the pipeline uses, exposed so the
/// law behind a decision can be checked — or searched directly.
///
/// Super-admin only. The citation chips on the claim page still deep-link here, so a
/// handler following one gets a 403; drop those chips to plain text if that matters.
[Authorize(Policy = Auth.SuperAdminPolicy)]
public class LegalModel : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? Query { get; set; }
    [BindProperty(SupportsGet = true, Name = "asOf")] public DateOnly? AsOf { get; set; }
    [BindProperty(SupportsGet = true, Name = "class")] public string? DocClass { get; set; }

    /// Deep link target. Citation chips anywhere in the app point at
    /// /legal?cite=<chunkId>, which is stable whether or not the passage happens to
    /// be rendered on the page the reader came from.
    [BindProperty(SupportsGet = true, Name = "cite")] public string? Cite { get; set; }

    public LegalHit? Focused { get; private set; }

    public List<LegalHit> Results { get; private set; } = [];
    public List<LegalHit> Corpus { get; private set; } = [];
    public string? CorpusVersion { get; private set; }
    public int Chunks { get; private set; }
    public int Embedded { get; private set; }
    public bool Searched => !string.IsNullOrWhiteSpace(Query);

    /// Metadata filter values; labels come from I18n ("source.<value>").
    public static readonly string[] Classes =
        ["", "statute", "case_law", "market_agreement", "protocol", "kifid", "policy_wording"];

    public async Task OnGetAsync()
    {
        CorpusVersion = Db.ActiveCorpusVersion();
        (Chunks, Embedded) = Db.CorpusStats();
        AsOf ??= DateOnly.FromDateTime(DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(Cite)) Focused = Db.GetChunk(Cite);

        if (Searched)
        {
            var classes = string.IsNullOrWhiteSpace(DocClass) ? null : new[] { DocClass };
            Results = await Legal.RetrieveAsync(Query!, AsOf.Value, 12, classes);
        }
        else if (CorpusVersion is not null)
        {
            Corpus = Db.ListCorpus(CorpusVersion);
        }
    }
}
