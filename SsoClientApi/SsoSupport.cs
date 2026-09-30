using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace SsoClientApi;

/// <summary>
/// Settings for talking to the SSO Gateway. Bound from the "Sso" section of appsettings.json.
/// Issuer / Audience / SecretKey MUST be identical to the Gateway's "JwtSettings".
/// </summary>
public class SsoOptions
{
    public const string CookieName = "sso_token";
    public const string ExpiredFlag = "sso_token_expired";

    public string BaseUrl { get; set; } = "https://localhost:7281";       // where the Gateway runs
    public string ClientBaseUrl { get; set; } = "https://localhost:7080"; // where THIS app runs
    public string AppName { get; set; } = "MockClientApp";               // must equal the TenantApp name in the Gateway
    public string Issuer { get; set; } = "SSOGateway";                    // = Gateway JwtSettings:Issuer
    public string Audience { get; set; } = "SSOClientApps";               // = Gateway JwtSettings:Audience
    public string SecretKey { get; set; } = "";                           // = Gateway JwtSettings:SecretKey

    /// <summary>Where the Gateway sends the browser after login. Must equal TenantApp.ReturnUrl.</summary>
    public string CallbackUrl => $"{ClientBaseUrl.TrimEnd('/')}/callback";

    public string GatewayLoginUrl =>
        $"{BaseUrl.TrimEnd('/')}/Auth/Login?returnUrl={Uri.EscapeDataString(CallbackUrl)}";

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
    /// <summary>
    /// All client apps share one signing key and audience, so the tenant_app claim is the only
    /// thing that stops a token issued for app A from being accepted by app B.
    /// </summary>
    public static bool IsIssuedForApp(ClaimsPrincipal? principal, string expectedApp) =>
        string.Equals(principal?.FindFirst("tenant_app")?.Value, expectedApp, StringComparison.OrdinalIgnoreCase);
}