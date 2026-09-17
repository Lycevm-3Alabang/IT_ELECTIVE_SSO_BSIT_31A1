using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Gateway.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SeedData.AdminRole)]
public class GroupsController : Controller
{
    private readonly SsoDbContext _context;

    public GroupsController(SsoDbContext context)
    {
        _context = context;
    }

    // GET /Admin/Groups
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var groups = await _context.Groups
            .Include(g => g.TenantApp)
            .OrderBy(g => g.TenantApp.Name)
            .ThenBy(g => g.Level)
            .ToListAsync();

        return View(groups);
    }

    // POST /Admin/Groups/Create - auto-prefix [AppName]-[GroupName], save level
    [HttpPost]
    public async Task<IActionResult> Create(int tenantAppId, string name, int level)
    {
        var app = await _context.Tenants.FindAsync(tenantAppId);

        if (app == null)
        {
            ModelState.AddModelError("TenantAppId", "Select a registered app.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError("Name", "Group name is required.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Name = name;
            ViewBag.Level = level;
            await PopulateAppsAsync(tenantAppId);
            return View();
        }

        var group = new Group
        {
            Name = BuildGroupName(app!.Name, name),
            Level = level,
            TenantAppId = tenantAppId,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _context.Groups.Add(group);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

   

    // Fills the app dropdown. Only active, registered apps can own a group.
    private async Task PopulateAppsAsync(int? selectedId = null)
    {
        var apps = await _context.Tenants
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        ViewBag.Apps = new SelectList(apps, "Id", "Name", selectedId);
    }

    // Produces "[AppName]-[GroupName]". Idempotent on purpose: re-saving an
    // already-prefixed name (e.g. from the Edit form) won't double-prefix it.
    public static string BuildGroupName(string? appName, string? groupName)
    {
        var prefix = (appName ?? string.Empty).Trim();
        var suffix = (groupName ?? string.Empty).Trim();

        if (prefix.Length == 0)
        {
            return suffix;
        }

        if (suffix.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
        {
            return suffix;
        }

        return $"{prefix}-{suffix}";
    }

}