using Data;
using Gateway.Models;
using Gateway.Services;
using Microsoft.AspNetCore.Authorization;
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

    // ---------------------------------------------------------------- LOGIN

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl)
    {
        TenantApp? app = null;
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            app = await _returnUrlValidator.ValidateAsync(returnUrl);
            if (app == null) return View("UnapprovedApp");
        }

        // Already signed in at the Gateway? This is the "single" in single sign-on: skip the form.
        if (User.Identity?.IsAuthenticated == true)
        {
            var current = await _userManager.GetUserAsync(User);
            if (current != null && current.IsActive)
            {
                return app == null
                    ? RedirectToAction(nameof(Portal))
                    : await IssueTokenRedirectAsync(current, app);
            }
            await _signInManager.SignOutAsync();   // stale/suspended session
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

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null || !user.IsActive)
        {
            await _auditService.LogLogin(user?.Id, email, false, user == null ? "User not found" : "Account suspended", ip);
            ModelState.AddModelError("", user != null ? "Account Suspended. Contact your administrator." : "Invalid email or password.");
            return View();
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            await _auditService.LogLogin(user.Id, email, false, "Account locked out (too many failed attempts)", ip);
            ModelState.AddModelError("", "Too many failed attempts. Please try again in 15 minutes.");
            return View();
        }

        if (!result.Succeeded)
        {
            await _auditService.LogLogin(null, email, false, "Invalid credentials", ip);
            ModelState.AddModelError("", "Invalid email or password.");
            return View();
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        user.LastLoginAt = DateTime.Now;
        await _userManager.UpdateAsync(user);
        await _auditService.LogLogin(user.Id, email, true, null, ip);

        // Temporary password (new user or admin reset): must change it before going anywhere.
        if (user.MustChangePassword)
        {
            return RedirectToAction(nameof(ChangePassword), new { returnUrl });
        }

        // Came from a client app -> straight back to it with the JWT.
        // Came to the Gateway directly -> Portal decides (admin chooser / user's app).
        return app == null
            ? RedirectToAction(nameof(Portal))
            : await IssueTokenRedirectAsync(user, app);
    }

    // --------------------------------------------------------------- PORTAL

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Portal()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.IsActive)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        var isAdmin = User.IsInRole(SeedData.AdminRole);
        var apps = await GetAccessibleAppsAsync(user.Id);

        // Regular user with exactly one app: no chooser, go straight in.
        if (!isAdmin && apps.Count == 1)
        {
            var only = await _context.Tenants.FirstAsync(t => t.Id == apps[0].Id);
            return await IssueTokenRedirectAsync(user, only);
        }

        return View(new PortalViewModel
        {
            Email = user.Email ?? string.Empty,
            IsAdmin = isAdmin,
            Apps = apps
        });
    }

    // Called by the chooser page when the person clicks an app.
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Launch(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.IsActive)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        var apps = await GetAccessibleAppsAsync(user.Id);
        if (apps.All(a => a.Id != id)) return RedirectToAction(nameof(Portal));

        var app = await _context.Tenants.FirstAsync(t => t.Id == id);
        await _auditService.LogAction(user.Id, "AppLaunch", $"{user.Email} opened {app.Name}");
        return await IssueTokenRedirectAsync(user, app);
    }

    // Single logout: MVC's /logout sends the browser here too.
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ------------------------------------------------------- CHANGE PASSWORD

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword(string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword, string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        if (string.IsNullOrWhiteSpace(newPassword))
            ModelState.AddModelError("", "New password is required.");
        else if (newPassword != confirmPassword)
            ModelState.AddModelError("", "New passwords do not match.");
        else if (newPassword == currentPassword)
            ModelState.AddModelError("", "New password must be different from the temporary password.");

        if (!ModelState.IsValid) return View();

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View();
        }

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);
        await _signInManager.RefreshSignInAsync(user);   // password change rotates the security stamp
        await _auditService.LogAction(user.Id, "PasswordChanged", $"{user.Email} changed their temporary password.");

        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            var app = await _returnUrlValidator.ValidateAsync(returnUrl);
            if (app != null) return await IssueTokenRedirectAsync(user, app);
        }
        return RedirectToAction(nameof(Portal));
    }

    // -------------------------------------------------------------- HELPERS

    private async Task<IActionResult> IssueTokenRedirectAsync(ApplicationUser user, TenantApp app)
    {
        var userGroupsForApp = await _context.UserGroups
            .Where(ug => ug.UserId == user.Id)
            .Include(ug => ug.Group)
            .Select(ug => ug.Group!)
            .Where(g => g.TenantAppId == app.Id)
            .ToListAsync();

        var token = _jwtTokenService.CreateToken(user, app.Name ?? "", userGroupsForApp);
        var target = app.ReturnUrl!;            // the registered URL, never user-supplied text
        var sep = target.Contains('?') ? "&" : "?";
        return Redirect($"{target}{sep}token={token}");
    }

    // Active apps in which this user belongs to at least one group.
    private async Task<List<PortalApp>> GetAccessibleAppsAsync(string userId)
    {
        var rows = await _context.UserGroups
            .Where(ug => ug.UserId == userId)
            .Select(ug => new
            {
                AppId = ug.Group!.TenantApp.Id,
                AppName = ug.Group.TenantApp.Name,
                AppActive = ug.Group.TenantApp.IsActive,
                GroupName = ug.Group.Name
            })
            .ToListAsync();

        return rows
            .Where(r => r.AppActive)
            .GroupBy(r => r.AppId)
            .Select(g => new PortalApp
            {
                Id = g.Key,
                Name = g.First().AppName ?? string.Empty,
                Groups = g.Select(x => x.GroupName ?? string.Empty).ToList()
            })
            .OrderBy(a => a.Name)
            .ToList();
    }
}