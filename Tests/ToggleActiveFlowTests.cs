using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gateway.Areas.Admin.Controllers;
using Data;
using Models.Entities;
using Moq;
using Xunit;

namespace Data.Tests;

public class ToggleActiveFlowTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager() =>
        new(new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    private static SsoDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ToggleActive_AjaxCall_ReturnsJsonForFrontEnd()
    {
        var user = new ApplicationUser { Id = "u1", Email = "s@example.com", IsActive = true };
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);
        userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        var controller = new UsersController(userManagerMock.Object, NewInMemoryContext())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var result = await controller.ToggleActive("u1");

        var json = Assert.IsType<JsonResult>(result);
        var idProp = json.Value!.GetType().GetProperty("id")!.GetValue(json.Value);
        var isActiveProp = json.Value!.GetType().GetProperty("isActive")!.GetValue(json.Value);

        Assert.Equal("u1", idProp);
        Assert.Equal(false, isActiveProp);
    }
}