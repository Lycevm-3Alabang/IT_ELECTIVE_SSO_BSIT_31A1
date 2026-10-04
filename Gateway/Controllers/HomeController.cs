using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Gateway.Models;

namespace Gateway.Controllers;

public class HomeController : Controller
{
    // "/Home" and "/Home/Index" both go to the login page.
    // (Without explicit routes, "/Home" resolves to Home/Login, because Login is the default action.)
    [HttpGet("/Home")]
    [HttpGet("/Home/Index")]
    public IActionResult Index() => RedirectToAction("Login", "Auth");

    public IActionResult Privacy()
    {
        return View();
    }

    // Shown whenever ReturnUrlValidator rejects a returnUrl - the client app
    // either isn't registered, is disabled, or sent a malformed/missing URL.
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult UnapprovedApp(string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}