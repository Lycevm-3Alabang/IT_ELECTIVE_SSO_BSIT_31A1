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
}