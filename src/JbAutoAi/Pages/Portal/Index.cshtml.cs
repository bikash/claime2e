using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages.Portal;

[Authorize(Policy = Auth.CustomerPolicy)]
public class IndexModel : PageModel
{
    public List<Claim> Claims { get; private set; } = [];

    public void OnGet() =>
        Claims = Db.ListClaimsForPortalUser(Auth.UserId(User) ?? "");
}
