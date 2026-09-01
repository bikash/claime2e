using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages.Portal;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string Email { get; set; } = "";
    [BindProperty] public string? Phone { get; set; }
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string PasswordRepeat { get; set; } = "";

    public string? Error { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if ((Password ?? "").Length < 8) { Error = I18n.T("portal.passwordShort"); return Page(); }
        if (Password != PasswordRepeat) { Error = I18n.T("portal.passwordMismatch"); return Page(); }

        var user = Db.CreatePortalUser(Email.Trim(), Name.Trim(),
                                       string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
                                       Auth.HashPassword(Password));
        if (user is null) { Error = I18n.T("portal.emailTaken"); return Page(); }

        await HttpContext.SignInAsync(Auth.Scheme, Auth.CustomerPrincipal(user),
            new AuthenticationProperties { IsPersistent = true });
        return Redirect("/portal/new");
    }
}
