using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages.Portal;

[Authorize(Policy = Auth.CustomerPolicy)]
public class NewModel : PageModel
{
    [BindProperty] public string LicensePlate { get; set; } = "";
    [BindProperty] public string? PolicyNumber { get; set; }
    [BindProperty] public DateOnly? LossDate { get; set; }
    [BindProperty] public string LossLocation { get; set; } = "";
    [BindProperty] public string Description { get; set; } = "";
    [BindProperty] public bool ThirdPartyInvolved { get; set; }
    [BindProperty] public bool Injuries { get; set; }

    public void OnGet() => LossDate = DateOnly.FromDateTime(DateTime.UtcNow);

    public IActionResult OnPost()
    {
        var userId = Auth.UserId(User);
        if (userId is null) return Forbid();
        var user = Db.GetPortalUser(userId);
        if (user is null) return Forbid();

        if (!ModelState.IsValid) return Page();

        var id = Db.CreateClaim(new Claim
        {
            PolicyholderName = user.Name,
            PolicyNumber = PolicyNumber,
            LicensePlate = LicensePlate,
            LossDate = LossDate,
            LossLocation = LossLocation,
            Description = Description,
            ThirdPartyInvolved = ThirdPartyInvolved,
            Injuries = Injuries,
        }, userId);

        Db.AddCustomerActivity(id, "created", userId, null);
        return Redirect($"/portal/claims/{id}");
    }
}
