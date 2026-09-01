using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JbAutoAi.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    [BindProperty] public string Email { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }
    public List<Handler> DemoAccounts { get; private set; } = [];

    public void OnGet() => DemoAccounts = Db.ListHandlers();

    public async Task<IActionResult> OnPostAsync()
    {
        DemoAccounts = Db.ListHandlers();

        var handler = Db.GetHandlerByEmail(Email ?? "");
        var hash = handler is null ? null : Db.GetHandlerPasswordHash(handler.Id);

        // Verify even when the account is unknown, so a missing account and a wrong
        // password take the same time and cannot be told apart.
        var ok = Auth.VerifyPassword(Password ?? "", hash) && handler is not null;
        if (!ok || handler is null)
        {
            Error = I18n.T("login.failed");
            return Page();
        }

        await HttpContext.SignInAsync(Auth.Scheme, Auth.StaffPrincipal(handler),
            new AuthenticationProperties { IsPersistent = true });
        Db.TouchHandlerLogin(handler.Id);

        return Redirect(Safe(ReturnUrl) ?? "/");
    }

    /// Open-redirect guard: only same-site paths.
    internal static string? Safe(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") ? url : null;
}
