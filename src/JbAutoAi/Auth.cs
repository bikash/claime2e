using System.Security.Claims;
using System.Security.Cryptography;

// The claim model in this namespace shadows the security one.
using SecurityClaim = System.Security.Claims.Claim;

namespace JbAutoAi;

/// Password hashing and the two identities the app serves.
///
/// ponytail: PBKDF2 from the BCL, not ASP.NET Identity. Identity brings a user
/// store, role store, token providers, two-factor and a migration surface for
/// what is here a handlers table and a portal_users table. Upgrade path if SSO
/// lands (NFR-3 wants OIDC/SAML): the cookie scheme stays, only the sign-in
/// endpoints change.
public static class Auth
{
    public const string Scheme = "jbauto";
    public const string StaffPolicy = "Staff";
    public const string CustomerPolicy = "Customer";
    public const string SuperAdminPolicy = "SuperAdmin";
    public const string SuperAdminRole = "super_admin";

    public const string KindClaim = "jb:kind";       // "staff" | "customer"
    public const string StaffKind = "staff";
    public const string CustomerKind = "customer";

    const int Iterations = 210_000;                  // OWASP 2023 guidance for PBKDF2-SHA256
    const int SaltBytes = 16;
    const int KeyBytes = 32;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeyBytes);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool VerifyPassword(string password, string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2") return false;
        if (!int.TryParse(parts[1], out var iterations)) return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations,
                                                   HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static ClaimsPrincipal StaffPrincipal(Handler h) =>
        new(new ClaimsIdentity(
        [
            new SecurityClaim(ClaimTypes.NameIdentifier, h.Id),
            new SecurityClaim(ClaimTypes.Name, h.Name),
            new SecurityClaim(ClaimTypes.Email, h.Email),
            new SecurityClaim(ClaimTypes.Role, h.Role),
            new SecurityClaim(KindClaim, StaffKind),
        ], Scheme));

    public static ClaimsPrincipal CustomerPrincipal(PortalUser u) =>
        new(new ClaimsIdentity(
        [
            new SecurityClaim(ClaimTypes.NameIdentifier, u.Id),
            new SecurityClaim(ClaimTypes.Name, u.Name),
            new SecurityClaim(ClaimTypes.Email, u.Email),
            new SecurityClaim(KindClaim, CustomerKind),
        ], Scheme));

    public static bool IsStaff(ClaimsPrincipal? p) => p?.FindFirstValue(KindClaim) == StaffKind;
    public static bool IsSuperAdmin(ClaimsPrincipal? p) =>
        IsStaff(p) && p?.FindFirstValue(ClaimTypes.Role) == SuperAdminRole;
    public static bool IsCustomer(ClaimsPrincipal? p) => p?.FindFirstValue(KindClaim) == CustomerKind;
    public static string? UserId(ClaimsPrincipal? p) => p?.FindFirstValue(ClaimTypes.NameIdentifier);
    public static string UserName(ClaimsPrincipal? p) => p?.FindFirstValue(ClaimTypes.Name) ?? "";
    public static string UserRole(ClaimsPrincipal? p) => p?.FindFirstValue(ClaimTypes.Role) ?? "";
}
