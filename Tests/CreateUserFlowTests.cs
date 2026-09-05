using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gateway.Areas.Admin.Controllers;
using Data;
using Models.Entities;
using Moq;
using Xunit;

namespace Data.Tests;

public class CreateUserFlowTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager() =>
        new(new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    private static SsoDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Create_PasswordMismatch_ReturnsErrorOnConfirmPassword()
    {
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByEmailAsync("newuser@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var controller = new UsersController(userManagerMock.Object, NewInMemoryContext());

        var result = await controller.Create("newuser@example.com", "Password123!", "Password124!");

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("ConfirmPassword"));
    }

    [Fact]
    public async Task Create_ValidData_RedirectsToIndex()
    {
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByEmailAsync("student@example.com"))
            .ReturnsAsync((ApplicationUser?)null);
        userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Passw0rd!"))
            .ReturnsAsync(IdentityResult.Success);

        var controller = new UsersController(userManagerMock.Object, NewInMemoryContext());

        var result = await controller.Create("student@example.com", "Passw0rd!", "Passw0rd!");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }
}