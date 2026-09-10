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
            await _context.SaveChangesAsync();
            return;
        }
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
        await _context.SaveChangesAsync();
    }
}