using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Gateway.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SeedData.AdminRole)]
public class TenantAppsController : Controller
{
    private readonly SsoDbContext _context;

    public TenantAppsController(SsoDbContext context)
    {
        _context = context;
    }
}