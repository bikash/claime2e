using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

[Authorize(Policy = Auth.SuperAdminPolicy)]
public class LegalAdminModel : PageModel
{
    public string Corpus { get; private set; } = "";
    public List<LegalDoc> Docs { get; private set; } = [];
    public LegalDoc Editing { get; private set; } = new();
    public string? Notice { get; private set; }

    [BindProperty] public LegalDoc Form { get; set; } = new();

    public void OnGet(string? edit, string? notice)
    {
        Corpus = Db.ActiveCorpusVersion() ?? "v1.0.0";
        Docs = Db.ListLegalDocs(Corpus);
        Notice = notice;
        if (edit is { Length: > 0 } && Db.GetLegalDoc(edit) is { } doc)
            Editing = doc;
        else
            Editing = new LegalDoc { CorpusVersion = Corpus, ValidFrom = new DateOnly(1970, 1, 1) };
    }

    public IActionResult OnPostSave()
    {
        if (string.IsNullOrWhiteSpace(Form.Id) || string.IsNullOrWhiteSpace(Form.Citation)
            || string.IsNullOrWhiteSpace(Form.Title) || string.IsNullOrWhiteSpace(Form.Passage))
            return Redirect("/legal/admin?notice=missing");

        if (string.IsNullOrWhiteSpace(Form.CorpusVersion))
            Form.CorpusVersion = Db.ActiveCorpusVersion() ?? "v1.0.0";

        Db.UpsertLegalDoc(Form);
        Db.ReplaceLegalDocChunk(Form.Id, Form.Passage);
        return Redirect($"/legal/admin?notice=saved");
    }

    public IActionResult OnPostDelete(string id)
    {
        if (!string.IsNullOrWhiteSpace(id)) Db.DeleteLegalDoc(id);
        return Redirect("/legal/admin?notice=deleted");
    }

    public async Task<IActionResult> OnPostEmbedAsync()
    {
        var n = await Legal.EmbedCorpusAsync();
        return Redirect($"/legal/admin?notice=embedded:{n}");
    }
}
