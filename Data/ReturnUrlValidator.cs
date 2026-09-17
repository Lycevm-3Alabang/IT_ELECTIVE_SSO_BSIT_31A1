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

    public async Task<TenantApp?> ValidateAsync(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            await _auditService.LogAction(
                userId: null,
                action: "UnapprovedReturnUrl",
                details: "Rejected returnUrl: value was missing or empty.");
            return null;
        }

        var normalizedReturnUrl = returnUrl.Trim();

        var app = await _context.Tenants
            .Where(t => t.IsActive
                        && t.ReturnUrl != null
                        && t.ReturnUrl.ToLower() == normalizedReturnUrl.ToLower())
            .FirstOrDefaultAsync();

        if (app == null)
        {
            await _auditService.LogAction(
                userId: null,
                action: "UnapprovedReturnUrl",
                details: $"Rejected unregistered or inactive returnUrl: {normalizedReturnUrl}");
            return null;
        }

        return app;
    }
}
