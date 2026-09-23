using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gateway.Areas.Admin.Controllers;
using Data;
using Models.Entities;
using Xunit;

namespace Data.Tests;

public class GroupsControllerAutoPrefixTests
{
    private static SsoDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<TenantApp> SeedAppAsync(SsoDbContext context, string name)
    {
        var app = new TenantApp
        {
            Name = name,
            ReturnUrl = "https://example.com/callback",
            IsActive = true
        };
        context.Tenants.Add(app);
        await context.SaveChangesAsync();
        return app;
    }

    [Fact]
    public async Task Create_ValidGroup_PrefixesNameWithAppName()
    {
        var context = NewInMemoryContext();
        var app = await SeedAppAsync(context, "SalesApp");
        var controller = new GroupsController(context);

        var result = await controller.Create(app.Id, "Admin", 0);

        Assert.IsType<RedirectToActionResult>(result);

        var saved = await context.Groups.SingleAsync();
        Assert.Equal("SalesApp-Admin", saved.Name);
        Assert.Equal(0, saved.Level);
        Assert.Equal(app.Id, saved.TenantAppId);
    }

    [Fact]
    public void BuildGroupName_AlreadyPrefixed_DoesNotPrefixTwice()
    {
        var result = GroupsController.BuildGroupName("SalesApp", "SalesApp-Admin");

        Assert.Equal("SalesApp-Admin", result);
    }

    [Fact]
    public async Task Create_DuplicateNameForSameApp_ReturnsErrorOnName()
    {
        var context = NewInMemoryContext();
        var app = await SeedAppAsync(context, "SalesApp");
        var controller = new GroupsController(context);

        await controller.Create(app.Id, "Admin", 0);
        var result = await controller.Create(app.Id, "Admin", 1);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Name"));
        Assert.Single(context.Groups);
    }
}