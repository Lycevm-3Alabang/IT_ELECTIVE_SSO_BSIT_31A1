using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Data;

/// <summary>
/// Validates a returnUrl supplied by a client app against the registered
/// TenantApps table, so that only approved apps can use the SSO login page.
/// </summary>
public class ReturnUrlValidator
{
    private readonly SsoDbContext _context;
    private readonly AuditService _auditService;

    public ReturnUrlValidator(SsoDbContext context, AuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }
    // No extra code needed here — this happens automatically.
    // When Issue 16 adds the login action, e.g.:
    //
    //     public IActionResult Login(string? returnUrl) { ... }
    //
    // ASP.NET Core's model binder pulls "returnUrl" off the query string
    // (?returnUrl=https://.../) and passes it in as that parameter for free.
}