using Data;
using Gateway.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Gateway.Controllers;

public class AuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SsoDbContext _context;
    private readonly ReturnUrlValidator _returnUrlValidator;
    private readonly AuditService _auditService;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
        SsoDbContext context, ReturnUrlValidator returnUrlValidator, AuditService auditService, JwtTokenService jwtTokenService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;
        _returnUrlValidator = returnUrlValidator;
        _auditService = auditService;
        _jwtTokenService = jwtTokenService;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl)
    {
        TenantApp? app = null;
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            app = await _returnUrlValidator.ValidateAsync(returnUrl);
            if (app == null) return View("UnapprovedApp");
        }

        ViewBag.AppName = app?.Name;
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl)
    {
        TenantApp? app = null;
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            app = await _returnUrlValidator.ValidateAsync(returnUrl);
            if (app == null) return View("UnapprovedApp");
        }
        ViewBag.AppName = app?.Name;
        ViewBag.ReturnUrl = returnUrl;

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError("", user != null ? "Account Suspended. Contact your administrator." : "Invalid email or password.");
            return View();
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View();
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        if (app == null)
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        var userGroupsForApp = await _context.UserGroups
            .Where(ug => ug.UserId == user.Id)
            .Include(ug => ug.Group)
            .Select(ug => ug.Group!)
            .Where(g => g.TenantAppId == app.Id)
            .ToListAsync();

        var token = _jwtTokenService.CreateToken(user, app.Name ?? "", userGroupsForApp);
        var sep = returnUrl!.Contains('?') ? "&" : "?";
        return Redirect($"{returnUrl}{sep}token={token}");
    }

}
