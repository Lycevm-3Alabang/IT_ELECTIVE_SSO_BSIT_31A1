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

}