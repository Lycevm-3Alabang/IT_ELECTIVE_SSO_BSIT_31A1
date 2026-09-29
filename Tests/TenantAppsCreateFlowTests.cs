using Gateway.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Xunit;

namespace Data.Tests;

public class TenantAppsCreateFlowTests
{
    private static SsoDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateFlow_ValidInput_RedirectsToIndexAndListsNewApp()
    {
        var context = NewInMemoryContext();
        var controller = new TenantAppsController(context);

        var createResult = await controller.Create("StudentPortal", "https://students.example.com/callback");
        Assert.IsType<RedirectToActionResult>(createResult);

        var indexResult = await controller.Index();
        var view = Assert.IsType<ViewResult>(indexResult);
        var apps = Assert.IsAssignableFrom<IEnumerable<TenantApp>>(view.Model);

        Assert.Contains(apps, a => a.Name == "StudentPortal" && a.IsActive);
    }

    [Fact]
    public async Task CreateFlow_MissingReturnUrl_ReturnsFormWithValidationError()
    {
        var context = NewInMemoryContext();
        var controller = new TenantAppsController(context);

        var result = await controller.Create("ReportsApp", "");

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState["ReturnUrl"]!.Errors.Count > 0);
        Assert.Empty(context.Tenants);
    }
}
