using Gateway.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Moq;
using Xunit;

namespace Data.Tests;

public class UsersControllerCreateWithGroupsTests
{
    private static (UsersController Controller, Mock<UserManager<ApplicationUser>> Um, SsoDbContext Db) Build()
    {
        var um = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        um.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        um.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        um.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        var db = new SsoDbContext(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var controller = new UsersController(um.Object, db, new AuditService(db))
        {
            TempData = new Mock<ITempDataDictionary>().Object
        };
        return (controller, um, db);
    }

    private static Group AddGroup(SsoDbContext db, string appName, string groupName, int level)
    {
        var app = new TenantApp { Name = appName, ReturnUrl = $"https://{appName}.example.com/callback", IsActive = true };
        db.Tenants.Add(app);
        db.SaveChanges();
        var group = new Group { Name = groupName, Level = level, TenantAppId = app.Id };
        db.Groups.Add(group);
        db.SaveChanges();
        return group;
    }

    [Fact]
    public async Task Create_WithGatewayAdminChecked_AddsAdminRole()
    {
        var (controller, um, _) = Build();

        var result = await controller.Create("boss@test.com", "Temp@12345", "Temp@12345", isAdmin: true);

        Assert.IsType<RedirectToActionResult>(result);
        um.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), SeedData.AdminRole), Times.Once);
    }

    [Fact]
    public async Task Create_WithoutGatewayAdminChecked_DoesNotAddAdminRole()
    {
        var (controller, um, _) = Build();

        await controller.Create("student@test.com", "Temp@12345", "Temp@12345");

        um.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithGroups_AssignsTheUserToThoseGroups()
    {
        var (controller, _, db) = Build();
        var users = AddGroup(db, "MvcClientApp", "MvcClientApp-Users", 1);
        var admins = AddGroup(db, "OtherApp", "OtherApp-Admin", 0);

        await controller.Create("multi@test.com", "Temp@12345", "Temp@12345",
            groupIds: new[] { users.Id, admins.Id });

        var assigned = await db.UserGroups.Select(ug => ug.GroupId).ToListAsync();
        Assert.Equal(2, assigned.Count);
        Assert.Contains(users.Id, assigned);
        Assert.Contains(admins.Id, assigned);
    }

    [Fact]
    public async Task Create_IgnoresGroupIdsThatDoNotExist()
    {
        var (controller, _, db) = Build();

        var result = await controller.Create("ghost@test.com", "Temp@12345", "Temp@12345",
            groupIds: new[] { 9999 });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(db.UserGroups);
    }

    [Fact]
    public async Task Create_MarksTemporaryPasswordByDefault_AndRespectsTheCheckbox()
    {
        var (controller, um, _) = Build();

        await controller.Create("a@test.com", "Temp@12345", "Temp@12345");
        await controller.Create("b@test.com", "Temp@12345", "Temp@12345", mustChangePassword: false);

        um.Verify(m => m.CreateAsync(
            It.Is<ApplicationUser>(u => u.Email == "a@test.com" && u.MustChangePassword), "Temp@12345"), Times.Once);
        um.Verify(m => m.CreateAsync(
            It.Is<ApplicationUser>(u => u.Email == "b@test.com" && !u.MustChangePassword), "Temp@12345"), Times.Once);
    }

    [Fact]
    public async Task Create_DuplicateEmail_StaysOnFormAndAssignsNothing()
    {
        var (controller, um, db) = Build();
        var group = AddGroup(db, "MvcClientApp", "MvcClientApp-Users", 1);
        um.Setup(m => m.FindByEmailAsync("taken@test.com"))
          .ReturnsAsync(new ApplicationUser { Email = "taken@test.com" });

        var result = await controller.Create("taken@test.com", "Temp@12345", "Temp@12345",
            isAdmin: true, groupIds: new[] { group.Id });

        Assert.IsType<ViewResult>(result);
        um.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        um.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        Assert.Empty(db.UserGroups);
    }
}