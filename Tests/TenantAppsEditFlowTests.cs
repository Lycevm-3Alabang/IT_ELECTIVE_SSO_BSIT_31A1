using Gateway.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Xunit;

namespace Data.Tests;

public class TenantAppsEditFlowTests
{
    private static SsoDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task EditFlow_ValidChanges_UpdatesNameAndReturnUrl()
    {
        var context = NewInMemoryContext();
        var app = new TenantApp { Name = "SalesApp", ReturnUrl = "https://old.example.com/", IsActive = true };
        context.Tenants.Add(app);
        await context.SaveChangesAsync();

        var controller = new TenantAppsController(context);
        var result = await controller.Edit(app.Id, "SalesApp-v2", "https://new.example.com/callback");

        Assert.IsType<RedirectToActionResult>(result);
        var saved = await context.Tenants.FindAsync(app.Id);
        Assert.Equal("SalesApp-v2", saved!.Name);
        Assert.Equal("https://new.example.com/callback", saved.ReturnUrl);
    }

    [Fact]
    public async Task EditFlow_InvalidReturnUrl_ReturnsFormWithValidationErrorAndKeepsOldValue()
    {
        var context = NewInMemoryContext();
        var app = new TenantApp { Name = "HrApp", ReturnUrl = "https://hr.example.com/", IsActive = true };
        context.Tenants.Add(app);
        await context.SaveChangesAsync();

        var controller = new TenantAppsController(context);
        var result = await controller.Edit(app.Id, "HrApp", "not-a-url");

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        var saved = await context.Tenants.FindAsync(app.Id);
        Assert.Equal("https://hr.example.com/", saved!.ReturnUrl);
    }

    [Fact]
    public async Task EditFlow_DeleteAfterEdit_RemovesApp()
    {
        var context = NewInMemoryContext();
        var app = new TenantApp { Name = "TempApp", ReturnUrl = "https://temp.example.com/", IsActive = true };
        context.Tenants.Add(app);
        await context.SaveChangesAsync();

        var controller = new TenantAppsController(context);
        await controller.Edit(app.Id, "TempApp-renamed", "https://temp.example.com/callback");
        await controller.Delete(app.Id);

        Assert.Empty(await context.Tenants.ToListAsync());
    }

    [Fact]
    public async Task EditFlow_UnknownId_ReturnsNotFound()
    {
        var context = NewInMemoryContext();
        var controller = new TenantAppsController(context);

        var result = await controller.Edit(999, "Ghost", "https://ghost.example.com/");

        Assert.IsType<NotFoundResult>(result);
    }
}