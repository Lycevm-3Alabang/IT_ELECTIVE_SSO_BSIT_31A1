using Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class AuditServiceLoginTests
{
    private static SsoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SsoDbContext(options);
    }

    [Fact]
    public async Task LogLogin_Success_CreatesAuditLogEntry()
    {
        var context = CreateContext();
        var service = new AuditService(context);

        await service.LogLogin("user-123", "test@example.com", success: true, reason: null, ipAddress: "127.0.0.1");

        var log = Assert.Single(context.AuditLogs);
        Assert.Equal("LoginSuccess", log.Action);
        Assert.Equal("user-123", log.UserId);
        Assert.Contains("test@example.com", log.Details);
    }

    [Fact]
    public async Task LogLogin_Failure_CreatesAuditLogEntryWithReason()
    {
        var context = CreateContext();
        var service = new AuditService(context);

        await service.LogLogin(null, "bad@example.com", success: false, reason: "Invalid password", ipAddress: "10.0.0.5");

        var log = Assert.Single(context.AuditLogs);
        Assert.Equal("LoginFailed", log.Action);
        Assert.Contains("Invalid password", log.Details);
        Assert.Equal("10.0.0.5", log.IpAddress);
    }
}