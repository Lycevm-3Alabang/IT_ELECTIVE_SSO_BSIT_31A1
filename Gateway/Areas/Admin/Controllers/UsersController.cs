using Microsoft.AspNetCore.Mvc.Rendering;
using Data;
using Gateway.Areas.Admin.Models;
using Gateway.Areas.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Gateway.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SeedData.AdminRole)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SsoDbContext _context;
    private readonly AuditService _auditService;

    public UsersController(UserManager<ApplicationUser> userManager, SsoDbContext context, AuditService auditService)
    {
        _userManager = userManager;
        _context = context;
        _auditService = auditService;
    }

    // GET /Admin/Users
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10)
    {
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.Email != null && u.Email.Contains(search));
        }

        query = query.OrderByDescending(u => u.CreatedAt);

        var totalUsers = await query.CountAsync();

        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);
        ViewBag.Search = search;

        return View(users);
    }

    // GET /Admin/Users/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateGroupsAsync();
        return View();
    }

    // POST /Admin/Users/Create - create user with hashed password
    [HttpPost]
    public async Task<IActionResult> Create(
        string email, string password, string confirmPassword,
        bool isAdmin = false, bool mustChangePassword = true, int[]? groupIds = null)
    {
        groupIds ??= Array.Empty<int>();

        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError("Email", "Email is required.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError("Password", "Temporary password is required.");
        }

        if (password != confirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "Email is already registered.");
            }
        }

        if (!ModelState.IsValid)
        {
            return await ReturnCreateViewAsync(email, isAdmin, mustChangePassword, groupIds);
        }

        // Only accept group ids that really exist.
        var validGroupIds = await _context.Groups
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => g.Id)
            .ToListAsync();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = mustChangePassword,
            CreatedAt = DateTime.Now
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return await ReturnCreateViewAsync(email, isAdmin, mustChangePassword, groupIds);
        }

        if (isAdmin)
        {
            var roleResult = await _userManager.AddToRoleAsync(user, SeedData.AdminRole);
            if (!roleResult.Succeeded)
            {
                TempData["Warning"] = $"User created, but the Admin role could not be assigned: " +
                                      string.Join("; ", roleResult.Errors.Select(e => e.Description));
            }
        }

        foreach (var gid in validGroupIds)
        {
            _context.UserGroups.Add(new UserGroup { UserId = user.Id, GroupId = gid });
        }

        if (validGroupIds.Count > 0)
        {
            await _context.SaveChangesAsync();
        }

        await _auditService.LogAction(user.Id, "UserCreated",
            $"Admin created account for {user.Email} (gatewayAdmin={isAdmin}, groups={validGroupIds.Count})");

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ReturnCreateViewAsync(string? email, bool isAdmin, bool mustChangePassword, int[] groupIds)
    {
        ViewBag.Email = email;
        ViewBag.IsAdmin = isAdmin;
        ViewBag.MustChangePassword = mustChangePassword;
        ViewBag.SelectedGroupIds = groupIds;
        await PopulateGroupsAsync();
        return View();
    }

    // Groups shown as checkboxes on the Create form, e.g. "MvcClientApp - MvcClientApp-Admin (level 0)".
    private async Task PopulateGroupsAsync()
    {
        ViewBag.AvailableGroups = await _context.Groups
            .Include(g => g.TenantApp)
            .Where(g => g.TenantApp.IsActive)
            .OrderBy(g => g.TenantApp.Name).ThenBy(g => g.Level)
            .Select(g => new AvailableGroupInfo
            {
                GroupId = g.Id,
                AppName = g.TenantApp.Name ?? string.Empty,
                GroupName = g.Name ?? string.Empty,
                Level = g.Level
            })
            .ToListAsync();
    }

    // GET /Admin/Users/Details/{id} - show user details
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var groups = await _context.UserGroups
            .Where(ug => ug.UserId == id)
            .Include(ug => ug.Group)
            .ThenInclude(g => g!.TenantApp)
            .Select(ug => new UserGroupInfo
            {
                GroupId = ug.GroupId,
                AppName = ug.Group!.TenantApp.Name ?? string.Empty,
                GroupName = ug.Group.Name ?? string.Empty,
                Level = ug.Group.Level
            })
            .ToListAsync();

        var model = new UserDetailsViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            IsAdmin = await _userManager.IsInRoleAsync(user, SeedData.AdminRole),
            Groups = groups
        };

        return View(model);
    }

    // GET /Admin/Users/{userId}/Groups - list user's groups
    [HttpGet("Admin/Users/{userId}/Groups")]
    public async Task<IActionResult> Groups(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var assignedGroups = await _context.UserGroups
            .Where(ug => ug.UserId == userId)
            .Include(ug => ug.Group)
            .ThenInclude(g => g!.TenantApp)
            .Select(ug => new AssignedGroupInfo
            {
                GroupId = ug.GroupId,
                AppName = ug.Group!.TenantApp.Name ?? string.Empty,
                GroupName = ug.Group.Name ?? string.Empty,
                Level = ug.Group.Level
            })
            .OrderBy(g => g.AppName).ThenBy(g => g.GroupName)
            .ToListAsync();

        var assignedIds = assignedGroups.Select(g => g.GroupId).ToHashSet();

        var availableGroups = await _context.Groups
            .Include(g => g.TenantApp)
            .Where(g => !assignedIds.Contains(g.Id))
            .OrderBy(g => g.TenantApp.Name).ThenBy(g => g.Name)
            .Select(g => new AvailableGroupInfo
            {
                GroupId = g.Id,
                AppName = g.TenantApp.Name ?? string.Empty,
                GroupName = g.Name ?? string.Empty
            })
            .ToListAsync();

        var model = new UserGroupsViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            AssignedGroups = assignedGroups,
            AvailableGroups = availableGroups
        };

        return View(model);
    }

    // POST /Admin/Users/{userId}/Groups - assign user to group
    [HttpPost("Admin/Users/{userId}/Groups")]
    public async Task<IActionResult> AssignGroup(string userId, int groupId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var group = await _context.Groups.FindAsync(groupId);
        if (group == null) return NotFound();

        var alreadyAssigned = await _context.UserGroups
            .AnyAsync(ug => ug.UserId == userId && ug.GroupId == groupId);

        if (!alreadyAssigned)
        {
            _context.UserGroups.Add(new UserGroup { UserId = userId, GroupId = groupId });
            await _context.SaveChangesAsync();

            await _auditService.LogAction(userId, "GroupAssigned",
                $"Assigned {user.Email} to group '{group.Name}'");
        }

        return RedirectToAction(nameof(Groups), new { userId });
    }

    // DELETE /Admin/Users/{userId}/Groups/{groupId} - unassign
    [HttpDelete("Admin/Users/{userId}/Groups/{groupId}")]
    public async Task<IActionResult> UnassignGroup(string userId, int groupId)
    {
        var userGroup = await _context.UserGroups
            .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId);

        if (userGroup == null) return NotFound();

        var group = await _context.Groups.FindAsync(groupId);

        _context.UserGroups.Remove(userGroup);
        await _context.SaveChangesAsync();

        await _auditService.LogAction(userId, "GroupUnassigned",
            $"Removed group '{group?.Name}' from user {userId}");

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { userId, groupId });
        }

        return RedirectToAction(nameof(Groups), new { userId });
    }

    // POST /Admin/Users/SetAdmin/{id} - grant or revoke Gateway admin
    [HttpPost]
    public async Task<IActionResult> SetAdmin(string id, bool makeAdmin)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Don't let an admin lock themselves out of the Gateway.
        if (!makeAdmin && user.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "You cannot remove your own admin access.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var alreadyAdmin = await _userManager.IsInRoleAsync(user, SeedData.AdminRole);

        if (makeAdmin && !alreadyAdmin)
        {
            var result = await _userManager.AddToRoleAsync(user, SeedData.AdminRole);
            if (result.Succeeded)
            {
                await _auditService.LogAction(user.Id, "AdminGranted", $"Gateway admin granted to {user.Email}");
                TempData["Success"] = $"{user.Email} is now a Gateway admin. It takes effect the next time they sign in.";
            }
            else
            {
                TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
            }
        }
        else if (!makeAdmin && alreadyAdmin)
        {
            var result = await _userManager.RemoveFromRoleAsync(user, SeedData.AdminRole);
            if (result.Succeeded)
            {
                await _auditService.LogAction(user.Id, "AdminRevoked", $"Gateway admin revoked from {user.Email}");
                TempData["Success"] = $"{user.Email} is no longer a Gateway admin.";
            }
            else
            {
                TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
            }
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /Admin/Users/Delete/{id} - soft delete (set IsActive = false)
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        user.IsActive = false;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return RedirectToAction(nameof(Index));
    }

    // POST /Admin/Users/ToggleActive/{id}
    [HttpPost]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await _auditService.LogAction(user.Id, "ToggleActive", $"Set IsActive={user.IsActive} for {user.Email}");

        // AJAX request -> return JSON; form submit -> redirect
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { id = user.Id, isActive = user.IsActive });
        }

        return RedirectToAction(nameof(Index));
    }

    // POST /Admin/Users/ResetPassword/{id}
    [HttpPost]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, temporaryPassword);

        if (!result.Succeeded)
        {
            TempData["Error"] = "Password reset failed: " +
                                string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Details), new { id });
        }

        user.MustChangePassword = true;
        await _userManager.UpdateAsync(user);

        await _auditService.LogAction(user.Id, "PasswordReset", $"Temporary password issued for {user.Email} by admin.");

        TempData["TemporaryPassword"] = temporaryPassword;
        return RedirectToAction(nameof(Details), new { id });
    }
}