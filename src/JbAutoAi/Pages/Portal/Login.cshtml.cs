using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages.Portal;

[AllowAnonymous]
public class LoginModel : PageModel
{
    [BindProperty] public string Email { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }
    public string? DemoEmail { get; private set; }

    public void OnGet() => DemoEmail = Seed.DemoPortalEmail;

    public async Task<IActionResult> OnPostAsync()
    {
        DemoEmail = Seed.DemoPortalEmail;

        var user = Db.GetPortalUserByEmail(Email ?? "");
        // Verify regardless, so an unknown email and a wrong password are
        // indistinguishable in both response and timing.
        var ok = Auth.VerifyPassword(Password ?? "", user?.PasswordHash) && user is not null;
        if (!ok || user is null)
        {
            Error = I18n.T("login.failed");
            return Page();
        }

        await HttpContext.SignInAsync(Auth.Scheme, Auth.CustomerPrincipal(user),
            new AuthenticationProperties { IsPersistent = true });
        Db.TouchPortalLogin(user.Id);

        return Redirect(JbAutoAi.Pages.LoginModel.Safe(ReturnUrl) ?? "/portal");
    }
}
