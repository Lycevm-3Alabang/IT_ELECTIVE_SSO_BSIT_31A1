using Gateway.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Data;
using Xunit;

namespace Data.Tests;

public class TenantAppsControllerCreateTests
{
    private static SsoDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Create_ValidApp_SavesAndRedirects()
    {
        var context = NewInMemoryContext();
        var controller = new TenantAppsController(context);

        var result = await controller.Create("SalesApp", "https://salesapp.example.com/callback");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var saved = await context.Tenants.SingleAsync();
        Assert.Equal("SalesApp", saved.Name);
        Assert.Equal("https://salesapp.example.com/callback", saved.ReturnUrl);
        Assert.True(saved.IsActive);
    }

    [Fact]
    public async Task Create_InvalidReturnUrl_ReturnsViewWithErrorAndDoesNotSave()
    {
        var context = NewInMemoryContext();
        var controller = new TenantAppsController(context);

        var result = await controller.Create("BadApp", "not-a-url");

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(context.Tenants);
    }
}