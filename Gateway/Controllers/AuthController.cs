using Data;
using Gateway.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Models.Entities;

namespace Gateway.Controllers;

public class AuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SsoDbContext _context;
    private readonly ReturnUrlValidator _returnUrlValidator;
    private readonly AuditService _auditService;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
        SsoDbContext context, ReturnUrlValidator returnUrlValidator, AuditService auditService, JwtTokenService jwtTokenService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;
        _returnUrlValidator = returnUrlValidator;
        _auditService = auditService;
        _jwtTokenService = jwtTokenService;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }
}
