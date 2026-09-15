using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Gateway.Areas.Admin.Controllers;
using Models.Entities;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace Data.Tests;

public class UsersControllerToggleActiveTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager() =>
        new(new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    private static SsoDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ToggleActive_TrueToFalse_UpdatesUser()
    {
        var user = new ApplicationUser { Id = "1", Email = "test@example.com", IsActive = true };
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        var context = NewInMemoryContext();
        var auditService = new AuditService(context);
        var controller = new UsersController(userManagerMock.Object, context, auditService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        await controller.ToggleActive("1");

        Assert.False(user.IsActive);
        userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u => !u.IsActive)), Times.Once);
    }

    [Fact]
    public async Task ToggleActive_FalseToTrue_UpdatesUser()
    {
        var user = new ApplicationUser { Id = "2", Email = "test2@example.com", IsActive = false };
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByIdAsync("2")).ReturnsAsync(user);
        userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        var context = NewInMemoryContext();
        var auditService = new AuditService(context);
        var controller = new UsersController(userManagerMock.Object, context, auditService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        await controller.ToggleActive("2");

        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task ToggleActive_UserNotFound_ReturnsNotFound()
    {
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);

        var controller = new UsersController(userManagerMock.Object, null!, null!);

        var result = await controller.ToggleActive("missing");

        Assert.IsType<NotFoundResult>(result);
    }
}