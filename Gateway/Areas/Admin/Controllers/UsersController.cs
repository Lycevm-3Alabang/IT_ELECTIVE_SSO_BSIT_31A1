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
    public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
    {
        var query = _userManager.Users.OrderByDescending(u => u.CreatedAt);
        var totalUsers = await query.CountAsync();

        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);

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
    public async Task<IActionResult> Create(string email, string password)
    {
        // Validate duplicate email before create
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "Email is already registered.");
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
}