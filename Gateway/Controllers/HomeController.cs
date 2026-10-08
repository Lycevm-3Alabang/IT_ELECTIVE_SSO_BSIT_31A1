using System.Diagnostics;
using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gateway.Models;

namespace Gateway.Controllers;

public class HomeController : Controller
{
    private readonly SsoDbContext _db;

    public HomeController(SsoDbContext db)
    {
        _db = db;
    }

    // "/Home" and "/Home/Index" both go to the login page.
    // (Without explicit routes, "/Home" resolves to Home/Login, because Login is the default action.)
    [HttpGet("/Home")]
    [HttpGet("/Home/Index")]
    public IActionResult Index() => RedirectToAction("Login", "Auth");

    [Authorize]
    public async Task<IActionResult> Privacy()
    {
        // If a TenantApp is registered with this exact page as its return URL
        // and it is switched off, block the page.
        var currentUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}".TrimEnd('/');

        var inactiveReturnUrls = await _db.Tenants
            .AsNoTracking()
            .Where(a => !a.IsActive && a.ReturnUrl != null)
            .Select(a => a.ReturnUrl!)
            .ToListAsync();

        var isBlocked = inactiveReturnUrls.Any(u =>
            string.Equals(u.TrimEnd('/'), currentUrl, StringComparison.OrdinalIgnoreCase));

        if (isBlocked)
            return RedirectToAction(nameof(UnapprovedApp), new { returnUrl = currentUrl });

        return View();
    }

    // Shown whenever ReturnUrlValidator rejects a returnUrl - the client app
    // either isn't registered, is disabled, or sent a malformed/missing URL.
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult UnapprovedApp(string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View("~/Views/Auth/UnapprovedApp.cshtml");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}