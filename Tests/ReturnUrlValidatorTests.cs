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
}