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

        var controller = new UsersController(userManagerMock.Object);

        await controller.ToggleActive("1");

        Assert.False(user.IsActive);
        userManagerMock.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u => !u.IsActive)), Times.Once);
    }
}