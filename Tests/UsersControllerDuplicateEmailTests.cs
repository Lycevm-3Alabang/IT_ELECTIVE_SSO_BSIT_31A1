using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Gateway.Areas.Admin.Controllers;
using Models.Entities;
using Moq;
using Xunit;

namespace Data.Tests;

public class UsersControllerDuplicateEmailTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager() =>
        new(new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    [Fact]
    public async Task Create_DuplicateEmail_ReturnsViewWithModelError_AndDoesNotCreateUser()
    {
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByEmailAsync("existing@example.com"))
            .ReturnsAsync(new ApplicationUser { Email = "existing@example.com" });

        // Nulls are safe here because validation fails before the context or AuditService are touched
        var controller = new UsersController(userManagerMock.Object, null!, null!);

        var result = await controller.Create("existing@example.com", "Password123!", "Password123!");

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Email"));

        userManagerMock.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }
}