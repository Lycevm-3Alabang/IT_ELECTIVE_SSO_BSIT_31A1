using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using MvcClientApp.Services;

namespace MvcClientApp.Controllers;

public class HomeController : Controller
{
    private static readonly Dictionary<string, string> ErrorMessages = new()
    {
        ["expired"] = "Your SSO session has expired. Please log in again.",
        ["invalid_token"] = "The token from the SSO gateway could not be verified.",
        ["missing_token"] = "No token was received from the SSO gateway.",
        ["wrong_app"] = "That token was issued for a different app.",
        ["no_access"] = "Your account is not assigned to this app. Please contact your administrator."
    };

    public IActionResult Index(string? error)
    {
        if (HttpContext.Items.ContainsKey(SsoOptions.ExpiredFlag)) error = "expired";

        ViewBag.Error = error != null && ErrorMessages.TryGetValue(error, out var message) ? message : null;
        return View();
    }

    [Authorize]
    public IActionResult Profile() => View(UserInfoMapper.FromPrincipal(User));

    public IActionResult Error() => View();
}