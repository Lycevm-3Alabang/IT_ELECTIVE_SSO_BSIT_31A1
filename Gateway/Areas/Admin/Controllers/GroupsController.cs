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

}