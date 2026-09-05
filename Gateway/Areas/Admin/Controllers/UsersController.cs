using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Gateway.Areas.Admin.Controllers;

[Area("Admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
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
    public IActionResult Create()
    {
        return View();
    }

    // POST /Admin/Users/Create - create user with hashed password
    [HttpPost]
    public async Task<IActionResult> Create(string email, string password, string confirmPassword)
    {
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
            ViewBag.Email = email;
            return View();
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            ViewBag.Email = email;
            return View();
        }

        return RedirectToAction(nameof(Index));
    }

    // GET /Admin/Users/Details/{id} - show user details
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        return View(user);
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

        // AJAX request -> return JSON; form submit -> redirect
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { id = user.Id, isActive = user.IsActive });
        }

        return RedirectToAction(nameof(Index));
    }
}