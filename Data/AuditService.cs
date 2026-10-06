using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Data;

public class AuditService
{
    private readonly SsoDbContext _context;

    public AuditService(SsoDbContext context)
    {
        _context = context;
    }

    public async Task LogLogin(string? userId, string email, bool success, string? reason, string? ipAddress)
    {
        if (success)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "LoginSuccess",
                Details = $"Login succeeded for {email}",
                Timestamp = DateTime.Now
            });
        }
        else
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = "LoginFailed",
                Details = $"Login failed for {email}: {reason}",
                IpAddress = ipAddress,
                Timestamp = DateTime.Now
            });
        }
        await SaveAsync();
    }

    public async Task LogAction(string? userId, string action, string details)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            Details = details,
            Timestamp = DateTime.Now
        });
        await SaveAsync();
    }

    // The audit row shares a DbContext with the rest of the request, so SaveChanges also tries to
    // save any other pending change. If one of those is stale (e.g. a user row another request
    // already changed), drop only that stale change and save the audit row anyway.
    // An audit log must never be the reason a login fails.
    private async Task SaveAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries.Where(e => e.Entity is not AuditLog))
            {
                entry.State = EntityState.Detached;
            }
            await _context.SaveChangesAsync();
        }
    }
}