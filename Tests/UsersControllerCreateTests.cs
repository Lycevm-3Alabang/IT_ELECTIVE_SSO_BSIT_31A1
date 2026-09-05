using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Gateway.Areas.Admin.Controllers;
using Models.Entities;
using Moq;
using Xunit;

namespace Data.Tests;

public class UsersControllerCreateTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager() =>
        new(new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    [Fact]
    public async Task Create_ValidUser_CallsCreateAsyncAndRedirects()
    {
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByEmailAsync("newuser@example.com"))
            .ReturnsAsync((ApplicationUser?)null);
        userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Success);

        var controller = new UsersController(userManagerMock.Object, null!);

        var result = await controller.Create("newuser@example.com", "Password123!");

        userManagerMock.Verify(m => m.CreateAsync(
            It.Is<ApplicationUser>(u => u.Email == "newuser@example.com" && u.IsActive),
            "Password123!"), Times.Once);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    [Fact]
    public async Task Create_IdentityCreationFails_ReturnsViewWithErrors()
    {
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByEmailAsync("baduser@example.com"))
            .ReturnsAsync((ApplicationUser?)null);
        userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "weak"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak." }));

        var controller = new UsersController(userManagerMock.Object, null!);

        var result = await controller.Create("baduser@example.com", "weak");

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }
}