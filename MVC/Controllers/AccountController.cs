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
    public IActionResult Login() => Redirect(_sso.GatewayLoginUrl);

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

        var principal = new ClaimsPrincipal(result.ClaimsIdentity);

        if (!SsoTokenRules.IsIssuedForApp(principal, _sso.AppName))
            return Redirect("/?error=wrong_app");

        // Bagong check: kailangang nasa group ng app na ito ang user
        if (!SsoTokenRules.HasAppAccess(principal))
            return Redirect("/?error=no_access");

        var jwt = (JsonWebToken)result.SecurityToken;
        Response.Cookies.Append(SsoOptions.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero)
        });

        return Redirect("/Home/Profile");
    }

    [HttpGet("/logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(SsoOptions.CookieName);
        return Redirect("/");
    }
}