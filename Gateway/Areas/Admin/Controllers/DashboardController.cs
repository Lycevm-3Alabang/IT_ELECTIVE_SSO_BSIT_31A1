using Data;
using Gateway.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Gateway.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SeedData.AdminRole)]
public class DashboardController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SsoDbContext _context;

    public DashboardController(UserManager<ApplicationUser> userManager, SsoDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    // GET /Admin/Dashboard
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        var users = await _userManager.Users.AsNoTracking().ToListAsync();
        var groups = await _context.Groups.AsNoTracking().ToListAsync();
        var apps = await _context.Tenants.AsNoTracking().ToListAsync();
        var userGroups = await _context.UserGroups.AsNoTracking().ToListAsync();
        var auditLogs = await _context.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

        var model = new DashboardViewModel
        {
            TotalUsers = users.Count,
            NewUsersThisMonth = users.Count(u => u.CreatedAt >= startOfMonth),

            TotalGroups = groups.Count,
            NewGroupsThisMonth = groups.Count(g => g.CreatedAt >= startOfMonth),

            TotalApplications = apps.Count,
            NewApplicationsThisMonth = apps.Count(a => a.CreatedAt >= startOfMonth),

            ActiveUsers = users.Count(u => u.IsActive),
            ActiveUserCount = users.Count(u => u.IsActive),
            InactiveUserCount = users.Count(u => !u.IsActive),
        };

        model.NewlyActivatedThisMonth = auditLogs.Count(a =>
            a.Action == "ToggleActive" &&
            a.Timestamp >= startOfMonth &&
            (a.Details ?? string.Empty).Contains("IsActive=True"));

        model.RecentActivities = auditLogs.Take(5).Select(MapToActivity).ToList();

        var days = Enumerable.Range(0, 7)
            .Select(offset => DateTime.Today.AddDays(-6 + offset))
            .ToList();

        foreach (var day in days)
        {
            model.ActivityChartLabels.Add(day.ToString("MMM d"));

            var logsForDay = auditLogs.Where(a => a.Timestamp.Date == day).ToList();

            model.LoginAttemptsSeries.Add(logsForDay.Count(a => a.Action is "LoginSuccess" or "LoginFailed"));
            model.PasswordResetsSeries.Add(logsForDay.Count(a => a.Action == "PasswordReset"));
            model.UsersCreatedSeries.Add(logsForDay.Count(a => a.Action == "UserCreated"));
            model.UsersDeactivatedSeries.Add(logsForDay.Count(a =>
                a.Action == "ToggleActive" && (a.Details ?? string.Empty).Contains("IsActive=False")));
        }

        model.TopGroups = groups
            .Select(g => new NamedCount
            {
                Name = g.Name ?? "(unnamed)",
                Count = userGroups.Count(ug => ug.GroupId == g.Id)
            })
            .OrderByDescending(g => g.Count)
            .Take(4)
            .ToList();

        var loginCountsByUser = auditLogs
            .Where(a => a.Action == "LoginSuccess" && a.UserId != null)
            .GroupBy(a => a.UserId!)
            .ToDictionary(g => g.Key, g => g.Count());

        model.TopApplications = apps
            .Select(app =>
            {
                var appGroupIds = groups.Where(g => g.TenantAppId == app.Id)
                    .Select(g => g.Id)
                    .ToHashSet();

                var userIdsInApp = userGroups
                    .Where(ug => ug.UserId != null && appGroupIds.Contains(ug.GroupId))
                    .Select(ug => ug.UserId!)
                    .Distinct();

                var loginCount = userIdsInApp.Sum(uid => loginCountsByUser.GetValueOrDefault(uid, 0));

                return new NamedCount { Name = app.Name ?? "(unnamed)", Count = loginCount };
            })
            .OrderByDescending(a => a.Count)
            .Take(4)
            .ToList();

        return View(model);
    }

    private static RecentActivityItem MapToActivity(AuditLog log)
    {
        var (title, icon) = log.Action switch
        {
            "UserCreated" => ("User created", "user-add"),
            "ToggleActive" when (log.Details ?? string.Empty).Contains("IsActive=False")
                => ("User deactivated", "user-remove"),
            "ToggleActive" => ("User activated", "user-add"),
            "PasswordReset" => ("Password reset", "user-add"),
            "LoginSuccess" => ("User logged in", "user-add"),
            "LoginFailed" => ("Failed login attempt", "user-remove"),
            _ => (log.Action ?? "Activity", "default")
        };

        return new RecentActivityItem
        {
            Title = title,
            Description = log.Details ?? string.Empty,
            Timestamp = log.Timestamp,
            IconType = icon
        };
    }
}