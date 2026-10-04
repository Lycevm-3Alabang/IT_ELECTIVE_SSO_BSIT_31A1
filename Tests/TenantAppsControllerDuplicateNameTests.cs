using Gateway.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Data;
using Xunit;

namespace Data.Tests;

public class TenantAppsControllerDuplicateNameTests
{
    private static SsoDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Create_DuplicateName_RejectsAndDoesNotSave()
    {
        var context = NewInMemoryContext();
        context.Tenants.Add(new TenantApp
        {
            Name = "SalesApp",
            ReturnUrl = "https://salesapp.example.com/callback",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var controller = new TenantAppsController(context);

        var result = await controller.Create("salesapp", "https://otherurl.example.com/callback");

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Single(await context.Tenants.ToListAsync());
    }

    [Fact]
    public async Task Edit_DuplicateNameFromAnotherApp_Rejects()
    {
        var context = NewInMemoryContext();
        context.Tenants.Add(new TenantApp { Name = "SalesApp", ReturnUrl = "https://a.example.com/" });
        context.Tenants.Add(new TenantApp { Name = "HrApp", ReturnUrl = "https://b.example.com/" });
        await context.SaveChangesAsync();

        var hrApp = await context.Tenants.SingleAsync(t => t.Name == "HrApp");
        var controller = new TenantAppsController(context);

        var result = await controller.Edit(hrApp.Id, "SalesApp", "https://b.example.com");

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }
}