using Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class AuditServiceActionTests
{
    private static SsoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SsoDbContext(options);
    }

    [Fact]
    public async Task LogAction_CreatesAuditLogEntry()
    {
        var context = CreateContext();
        var service = new AuditService(context);

        await service.LogAction("user-123", "ToggleActive", "Set IsActive=false for test@example.com");

        var log = Assert.Single(context.AuditLogs);
        Assert.Equal("ToggleActive", log.Action);
        Assert.Equal("user-123", log.UserId);
        Assert.Contains("IsActive=false", log.Details);
    }

    [Fact]
    public async Task LogAction_MultipleActions_CreatesSeparateEntries()
    {
        var context = CreateContext();
        var service = new AuditService(context);

        await service.LogAction("user-1", "UserCreated", "Created account for a@example.com");
        await service.LogAction("user-1", "PasswordReset", "Temporary password issued");

        Assert.Equal(2, context.AuditLogs.Count());
    }
}
