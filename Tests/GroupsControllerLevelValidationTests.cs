using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gateway.Areas.Admin.Controllers;
using Data;
using Models.Entities;
using Xunit;

namespace Data.Tests;

public class GroupsControllerLevelValidationTests
{
    private static SsoDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<TenantApp> SeedAppAsync(SsoDbContext context)
    {
        var app = new TenantApp
        {
            Name = "SalesApp",
            ReturnUrl = "https://example.com/callback",
            IsActive = true
        };
        context.Tenants.Add(app);
        await context.SaveChangesAsync();
        return app;
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public async Task Create_LevelOutOfRange_ReturnsErrorOnLevel(int level)
    {
        var context = NewInMemoryContext();
        var app = await SeedAppAsync(context);
        var controller = new GroupsController(context);

        var result = await controller.Create(app.Id, "Manager", level);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Level"));
        Assert.Empty(context.Groups);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task Create_LevelInRange_SavesGroup(int level)
    {
        var context = NewInMemoryContext();
        var app = await SeedAppAsync(context);
        var controller = new GroupsController(context);

        var result = await controller.Create(app.Id, $"Role{level}", level);

        Assert.IsType<RedirectToActionResult>(result);

        var saved = await context.Groups.SingleAsync();
        Assert.Equal(level, saved.Level);
    }
}
