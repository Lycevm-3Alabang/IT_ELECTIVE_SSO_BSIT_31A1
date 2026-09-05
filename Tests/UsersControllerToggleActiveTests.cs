using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Gateway.Areas.Admin.Controllers;
using Models.Entities;
using Moq;
using Xunit;

namespace Data.Tests;

public class UsersControllerToggleActiveTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager() =>
        new(new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    // Task 7: toggle changes IsActive from true → false
    [Fact]
    public async Task ToggleActive_TrueToFalse_UpdatesUser()
    {
        var user = new ApplicationUser { Id = "1", IsActive = true };
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        var controller = new UsersController(userManagerMock.Object, null!);

        await controller.ToggleActive("1");

        Assert.False(user.IsActive);
        userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u => !u.IsActive)), Times.Once);
    }

    // Task 8: toggle changes IsActive from false → true
    [Fact]
    public async Task ToggleActive_FalseToTrue_UpdatesUser()
    {
        var user = new ApplicationUser { Id = "2", IsActive = false };
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByIdAsync("2")).ReturnsAsync(user);
        userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        var controller = new UsersController(userManagerMock.Object, null!);

        await controller.ToggleActive("2");

        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task ToggleActive_UserNotFound_ReturnsNotFound()
    {
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);

        var controller = new UsersController(userManagerMock.Object, null!);

        var result = await controller.ToggleActive("missing");

        Assert.IsType<NotFoundResult>(result);
    }
}