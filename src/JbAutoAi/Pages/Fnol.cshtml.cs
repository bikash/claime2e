using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

[Authorize(Policy = Auth.StaffPolicy)]
public class FnolModel : PageModel
{
    [BindProperty] public string PolicyholderName { get; set; } = "";
    [BindProperty] public string PolicyNumber { get; set; } = "";
    [BindProperty] public string LicensePlate { get; set; } = "";
    [BindProperty] public string? Vin { get; set; }
    [BindProperty] public DateOnly? LossDate { get; set; }
    [BindProperty] public string LossLocation { get; set; } = "";
    [BindProperty] public string Description { get; set; } = "";
    [BindProperty] public bool ThirdPartyInvolved { get; set; }
    [BindProperty] public bool Injuries { get; set; }
    [BindProperty] public string? PoliceReportNumber { get; set; }

    public void OnGet() => LossDate = DateOnly.FromDateTime(DateTime.UtcNow);

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid) return Page();

        var id = Db.CreateClaim(new Claim
        {
            PolicyholderName = PolicyholderName,
            PolicyNumber = PolicyNumber,
            LicensePlate = LicensePlate,
            Vin = Vin,
            LossDate = LossDate,
            LossLocation = LossLocation,
            Description = Description,
            ThirdPartyInvolved = ThirdPartyInvolved,
            Injuries = Injuries,
            PoliceReportNumber = PoliceReportNumber,
        });
        return Redirect($"/claims/{id}");
    }
}
