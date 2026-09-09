using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Gateway.Areas.Admin.Controllers;
using Models.Entities;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Data;

namespace Data.Tests;

public class UsersControllerResetPasswordTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager() =>
        new(new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    private static SsoDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ResetPassword_ValidUser_UpdatesPasswordAndRedirects()
    {
        var user = new ApplicationUser { Id = "u1", Email = "user@example.com" };

        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);
        userManagerMock.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("fake-token");
        userManagerMock.Setup(m => m.ResetPasswordAsync(user, "fake-token", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var controller = new UsersController(userManagerMock.Object, NewInMemoryContext());

        var result = await controller.ResetPassword("u1");

        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task ResetPassword_UserNotFound_ReturnsNotFound()
    {
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);

        var controller = new UsersController(userManagerMock.Object, NewInMemoryContext());

        var result = await controller.ResetPassword("missing");

        Assert.IsType<NotFoundResult>(result);
    }
}