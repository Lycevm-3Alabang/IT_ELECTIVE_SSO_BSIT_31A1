using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Gateway.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SeedData.AdminRole)]
public class TenantAppsController : Controller
{
    private readonly SsoDbContext _context;

    public TenantAppsController(SsoDbContext context)
    {
        _context = context;
    }

    // GET /Admin/TenantApps
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var apps = await _context.Tenants
            .OrderBy(t => t.Name)
            .ToListAsync();

        return View(apps);
    }

    // GET /Admin/TenantApps/Create
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    // POST /Admin/TenantApps/Create - save app name + return URL
    [HttpPost]
    public async Task<IActionResult> Create(string name, string returnUrl)
    {
        await ValidateAppAsync(name, returnUrl, excludeId: null);

        if (!ModelState.IsValid)
        {
            ViewBag.Name = name;
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        var app = new TenantApp
        {
            Name = name.Trim(),
            ReturnUrl = returnUrl.Trim(),
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _context.Tenants.Add(app);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // GET /Admin/TenantApps/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var app = await _context.Tenants.FindAsync(id);
        if (app == null) return NotFound();

        return View(app);
    }

    // POST /Admin/TenantApps/Edit/{id} - update app
    [HttpPost]
    public async Task<IActionResult> Edit(int id, string name, string returnUrl)
    {
        var app = await _context.Tenants.FindAsync(id);
        if (app == null) return NotFound();

        await ValidateAppAsync(name, returnUrl, excludeId: id);

        if (!ModelState.IsValid)
        {
            app.Name = name;
            app.ReturnUrl = returnUrl;
            return View(app);
        }

        app.Name = name.Trim();
        app.ReturnUrl = returnUrl.Trim();
        app.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // POST /Admin/TenantApps/Delete/{id} - remove app
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var app = await _context.Tenants.FindAsync(id);
        if (app == null) return NotFound();

        _context.Tenants.Remove(app);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // Shared validation used by Create and Edit
    private async Task ValidateAppAsync(string? name, string? returnUrl, int? excludeId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError("Name", "App name is required.");
        }
        else
        {
            var nameTaken = await _context.Tenants
                .AnyAsync(t => t.Name != null
                    && t.Name.ToLower() == name.Trim().ToLower()
                    && (excludeId == null || t.Id != excludeId));

            if (nameTaken)
            {
                ModelState.AddModelError("Name", "An app with this name is already registered.");
            }
        }

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            ModelState.AddModelError("ReturnUrl", "Return URL is required.");
        }
        else if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri)
                 || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ModelState.AddModelError("ReturnUrl", "Return URL must be a valid absolute http or https URL.");
        }
    }

}