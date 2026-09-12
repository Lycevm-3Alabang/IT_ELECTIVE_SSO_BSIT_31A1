using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AuditLogsController : Controller
{
    private readonly SsoDbContext _context;

    public AuditLogsController(SsoDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, int page = 1, int pageSize = 20)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.Timestamp <= to.Value.AddDays(1).AddTicks(-1));

        query = query.OrderByDescending(a => a.Timestamp);

        var totalLogs = await query.CountAsync();

        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalLogs / (double)pageSize);
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");

        return View(logs);
    }
}