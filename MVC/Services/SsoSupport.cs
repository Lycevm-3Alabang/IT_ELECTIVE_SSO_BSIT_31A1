using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace MvcClientApp.Services;

public class SsoOptions
{
    // Iba ang cookie name kaysa sa SsoClientApi: shared ang cookies ng lahat ng port sa localhost
    public const string CookieName = "mvc_sso_token";
    public const string ExpiredFlag = "mvc_sso_token_expired";

    public string BaseUrl { get; set; } = "https://localhost:7281";
    public string ClientBaseUrl { get; set; } = "https://localhost:7180";
    public string AppName { get; set; } = "MvcClientApp";
    public string Issuer { get; set; } = "SSOGateway";
    public string Audience { get; set; } = "SSOClientApps";
    public string SecretKey { get; set; } = "";

    public string CallbackUrl => $"{ClientBaseUrl.TrimEnd('/')}/callback";

    public string GatewayLoginUrl =>
        $"{BaseUrl.TrimEnd('/')}/Auth/Login?returnUrl={Uri.EscapeDataString(CallbackUrl)}";
    public string GatewayLogoutUrl => $"{BaseUrl.TrimEnd('/')}/Auth/Logout";

    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        RequireExpirationTime = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
}

public static class SsoTokenRules
{
    public const string AdminRole = "Admin";
    public const string UserRole = "User";

    // Para hindi gumana dito ang token na para sa ibang tenant app
    public static bool IsIssuedForApp(ClaimsPrincipal? principal, string expectedApp) =>
        string.Equals(principal?.FindFirst("tenant_app")?.Value, expectedApp, StringComparison.OrdinalIgnoreCase);

    // Dapat may kahit isang group ang user para sa app na ito
    public static bool HasAppAccess(ClaimsPrincipal? principal) =>
        !string.IsNullOrWhiteSpace(principal?.FindFirst("groups")?.Value);

    // Level 0 = highest power (same rule as the Gateway). Everyone is a User; level 0 adds Admin.
    public static IEnumerable<string> RolesFromLevels(string? levelsJson)
    {
        var roles = new List<string> { UserRole };
        if (!string.IsNullOrWhiteSpace(levelsJson))
        {
            try
            {
                var levels = JsonSerializer.Deserialize<Dictionary<string, int>>(levelsJson);
                if (levels != null && levels.Values.Any(l => l == 0))
                    roles.Add(AdminRole);
            }
            catch (JsonException) { }
        }
        return roles;
    }

    // Builds the identity stored in MVC's session cookie from the already-validated JWT.
    public static ClaimsIdentity BuildSessionIdentity(ClaimsIdentity validated, DateTimeOffset expiresAt)
    {
        string Get(string type) => validated.FindFirst(type)?.Value ?? "";

        var email = Get("email");
        var levels = Get("levels");
        if (string.IsNullOrWhiteSpace(levels)) levels = "{}";

        var claims = new List<Claim>
        {
            new("sub", Get("sub")),
            new("email", email),
            new(ClaimTypes.Name, email),
            new("tenant_app", Get("tenant_app")),
            new("groups", Get("groups")),
            new("levels", levels),
            new("exp", expiresAt.ToUnixTimeSeconds().ToString())
        };
        claims.AddRange(RolesFromLevels(levels).Select(r => new Claim(ClaimTypes.Role, r)));

        return new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimTypes.Name, ClaimTypes.Role);
    }
}

public record UserInfoResponse(
    string? Sub,
    string? Email,
    string? TenantApp,
    string[] Groups,
    Dictionary<string, int> Levels,
    DateTimeOffset? ExpiresAt);

public static class UserInfoMapper
{
    public static UserInfoResponse FromPrincipal(ClaimsPrincipal user)
    {
        var groups = (user.FindFirst("groups")?.Value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var levels = new Dictionary<string, int>();
        var levelsJson = user.FindFirst("levels")?.Value;
        if (!string.IsNullOrWhiteSpace(levelsJson))
        {
            try { levels = JsonSerializer.Deserialize<Dictionary<string, int>>(levelsJson) ?? levels; }
            catch (JsonException) { }
        }

        DateTimeOffset? expiresAt = long.TryParse(user.FindFirst("exp")?.Value, out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp)
            : null;

        return new UserInfoResponse(
            user.FindFirst("sub")?.Value,
            user.FindFirst("email")?.Value,
            user.FindFirst("tenant_app")?.Value,
            groups,
            levels,
            expiresAt);
    }
}