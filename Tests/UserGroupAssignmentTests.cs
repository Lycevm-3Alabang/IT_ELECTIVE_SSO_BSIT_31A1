using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Gateway.Areas.Admin.Controllers;
using Models.Entities;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace Data.Tests;

public class UserGroupAssignmentTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager() =>
        new(new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    private static SsoDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UsersController BuildController(SsoDbContext context, Mock<UserManager<ApplicationUser>> userManagerMock)
    {
        var auditService = new AuditService(context);
        return new UsersController(userManagerMock.Object, context, auditService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static async Task<(Group groupA, Group groupB)> SeedTwoAppsWithGroups(SsoDbContext context)
    {
        var appA = new TenantApp { Name = "SalesApp", ReturnUrl = "https://sales.example.com/callback" };
        var appB = new TenantApp { Name = "HrApp", ReturnUrl = "https://hr.example.com/callback" };
        context.Tenants.AddRange(appA, appB);
        await context.SaveChangesAsync();

        var groupA = new Group { Name = "SalesApp-Admin", Level = 0, TenantAppId = appA.Id };
        var groupB = new Group { Name = "HrApp-Manager", Level = 1, TenantAppId = appB.Id };
        context.Groups.AddRange(groupA, groupB);
        await context.SaveChangesAsync();

        return (groupA, groupB);
    }

    [Fact]
    public async Task AssignGroup_UserCanBeAssignedToMultipleGroupsAcrossDifferentApps()
    {
        var user = new ApplicationUser { Id = "u1", Email = "user1@example.com" };
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);

        var context = NewInMemoryContext();
        var (groupA, groupB) = await SeedTwoAppsWithGroups(context);
        var controller = BuildController(context, userManagerMock);

        await controller.AssignGroup("u1", groupA.Id);
        await controller.AssignGroup("u1", groupB.Id);

        var assignments = await context.UserGroups.Where(ug => ug.UserId == "u1").ToListAsync();

        Assert.Equal(2, assignments.Count);
        Assert.Contains(assignments, a => a.GroupId == groupA.Id);
        Assert.Contains(assignments, a => a.GroupId == groupB.Id);
    }
}