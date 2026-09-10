using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Areas.Admin.Controllers;

[Area("Admin")]
public class AuditLogsController : Controller
{
    private readonly SsoDbContext _context;

    public AuditLogsController(SsoDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var logs = await _context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

        return View(logs);
    }
}