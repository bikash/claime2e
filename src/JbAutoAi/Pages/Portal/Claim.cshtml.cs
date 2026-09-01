using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages.Portal;

[Authorize(Policy = Auth.CustomerPolicy)]
public class ClaimModel : PageModel
{
    public JbAutoAi.Claim Claim { get; private set; } = new();
    public List<Document> Documents { get; private set; } = [];
    public List<ActivityEntry> Timeline { get; private set; } = [];

    public IActionResult OnGet(string cid)
    {
        var userId = Auth.UserId(User);
        if (userId is null) return Forbid();

        // Ownership is part of the lookup — a guessed id is a 404, not a leak.
        var claim = Db.GetClaimForPortalUser(cid, userId);
        if (claim is null) return NotFound();

        Claim = claim;
        Documents = Db.GetDocuments(cid);
        Timeline = Db.ListCustomerActivity(cid);
        return Page();
    }
}
