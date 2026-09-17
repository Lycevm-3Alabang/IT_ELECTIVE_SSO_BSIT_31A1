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

    // GET /Admin/Groups/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateAppsAsync();
        return View();
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

        ValidateLevel(level);
        await ValidateNameUniqueAsync(app, name, excludeId: null);

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

    // GET /Admin/Groups/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var group = await _context.Groups
            .Include(g => g.TenantApp)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null) return NotFound();

        await PopulateAppsAsync(group.TenantAppId);
        return View(group);
    }

    // POST /Admin/Groups/Edit/{id} - update group
    [HttpPost]
    public async Task<IActionResult> Edit(int id, int tenantAppId, string name, int level)
    {
        var group = await _context.Groups.FindAsync(id);
        if (group == null) return NotFound();

        var app = await _context.Tenants.FindAsync(tenantAppId);

        if (app == null)
        {
            ModelState.AddModelError("TenantAppId", "Select a registered app.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError("Name", "Group name is required.");
        }

        ValidateLevel(level);
        await ValidateNameUniqueAsync(app, name, excludeId: id);

        if (!ModelState.IsValid)
        {
            group.Name = name;
            group.Level = level;
            group.TenantAppId = tenantAppId;
            await PopulateAppsAsync(tenantAppId);
            return View(group);
        }

        group.Name = BuildGroupName(app!.Name, name);
        group.Level = level;
        group.TenantAppId = tenantAppId;
        group.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // POST /Admin/Groups/Delete/{id} - remove group
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var group = await _context.Groups.FindAsync(id);
        if (group == null) return NotFound();

        _context.Groups.Remove(group);
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

    // 0 = highest power, higher numbers = less power.
    private void ValidateLevel(int level)
    {
        if (level < 0)
        {
            ModelState.AddModelError("Level", "Level must be 0 or greater (0 = highest power).");
        }
        else if (level > 99)
        {
            ModelState.AddModelError("Level", "Level must be 99 or less.");
        }
    }

    // The DB has a unique index on (Name, TenantAppId); this catches the
    // collision before EF throws, so the admin sees a real message.
    private async Task ValidateNameUniqueAsync(TenantApp? app, string? name, int? excludeId)
    {
        if (app == null || string.IsNullOrWhiteSpace(name)) return;

        var fullName = BuildGroupName(app.Name, name);

        var taken = await _context.Groups
            .AnyAsync(g => g.TenantAppId == app.Id
                && g.Name != null
                && g.Name.ToLower() == fullName.ToLower()
                && (excludeId == null || g.Id != excludeId));

        if (taken)
        {
            ModelState.AddModelError("Name", $"The group \"{fullName}\" already exists for this app.");
        }
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