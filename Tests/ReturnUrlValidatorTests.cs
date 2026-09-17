using Data;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Xunit;

namespace Data.Tests;

public class ReturnUrlValidatorTests
{
    private static SsoDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ValidateAsync_RegisteredActiveReturnUrl_ReturnsMatchingApp()
    {
        var context = NewInMemoryContext();
        context.Tenants.Add(new TenantApp
        {
            Name = "SalesApp",
            ReturnUrl = "https://salesapp.example.com/callback",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var validator = new ReturnUrlValidator(context, new AuditService(context));

        var result = await validator.ValidateAsync("https://salesapp.example.com/callback");

        Assert.NotNull(result);
        Assert.Equal("SalesApp", result!.Name);
    }

    [Fact]
    public async Task ValidateAsync_UnregisteredReturnUrl_ReturnsNull()
    {
        var context = NewInMemoryContext();
        context.Tenants.Add(new TenantApp
        {
            Name = "SalesApp",
            ReturnUrl = "https://salesapp.example.com/callback",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var validator = new ReturnUrlValidator(context, new AuditService(context));

        var result = await validator.ValidateAsync("https://not-registered.example.com/callback");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_UnapprovedReturnUrl_IsLoggedToAuditLogs()
    {
        var context = NewInMemoryContext();
        var validator = new ReturnUrlValidator(context, new AuditService(context));

        var result = await validator.ValidateAsync("https://unapproved.example.com/callback");

        Assert.Null(result);

        var log = await context.AuditLogs.SingleAsync();
        Assert.Equal("UnapprovedReturnUrl", log.Action);
        Assert.Contains("https://unapproved.example.com/callback", log.Details);
    }
}