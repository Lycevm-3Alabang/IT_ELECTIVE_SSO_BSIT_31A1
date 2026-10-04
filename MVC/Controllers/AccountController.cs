using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MvcClientApp.Services;
using System.Security.Claims;

namespace MvcClientApp.Controllers;

public class AccountController : Controller
{
    private readonly SsoOptions _sso;

    public AccountController(SsoOptions sso)
    {
        _sso = sso;
    }

    [HttpGet("/login")]
    public IActionResult Login() =>
        User.Identity?.IsAuthenticated == true
            ? Redirect("/Home/Profile")
            : Redirect(_sso.GatewayLoginUrl);

    [HttpGet("/callback")]
    public async Task<IActionResult> Callback(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Redirect("/?error=missing_token");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, _sso.CreateValidationParameters());
        if (!result.IsValid)
        {
            return Redirect(result.Exception is SecurityTokenExpiredException
                ? "/?error=expired"
                : "/?error=invalid_token");
        }

        var validated = new ClaimsPrincipal(result.ClaimsIdentity);

        if (!SsoTokenRules.IsIssuedForApp(validated, _sso.AppName))
            return Redirect("/?error=wrong_app");

        if (!SsoTokenRules.HasAppAccess(validated))
            return Redirect("/?error=no_access");

        var jwt = (JsonWebToken)result.SecurityToken;
        var expiresAt = new DateTimeOffset(DateTime.SpecifyKind(jwt.ValidTo, DateTimeKind.Utc));

        var identity = SsoTokenRules.BuildSessionIdentity(result.ClaimsIdentity, expiresAt);

        var props = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false,
            ExpiresUtc = expiresAt            // cookie session ends exactly when the JWT ends
        };
        // Keep the original JWT with the session, in case this app later needs to forward it to an API.
        props.StoreTokens(new[] { new AuthenticationToken { Name = "access_token", Value = token } });

        Response.Cookies.Delete(SsoOptions.CookieName);   // clean up the old raw-JWT cookie from the previous version
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), props);

        return Redirect("/Home/Profile");
    }

        // Single logout: clear MVC's session, then let the Gateway clear its own.
    // POST only, and only from this app's own pages (see RequestOriginGuard).
    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        if (!RequestOriginGuard.IsSameOrigin(Request))
            return BadRequest("Cross-site logout request blocked.");

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete(SsoOptions.CookieName);
        return Redirect(_sso.GatewayLogoutUrl);
    }
}