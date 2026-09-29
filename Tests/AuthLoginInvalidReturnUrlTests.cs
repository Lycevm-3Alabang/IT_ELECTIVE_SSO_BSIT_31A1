// Tests/AuthLoginInvalidReturnUrlTests.cs
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Data.Tests;

public class AuthLoginInvalidReturnUrlTests
{
    [Fact]
    public async Task LoginGet_UnregisteredReturnUrl_ShowsUnapprovedAppPage()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp(); // registered: client.example.com only

        var result = await ctx.Controller.Login("https://evil.example.com/steal");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("UnapprovedApp", view.ViewName);
    }

    [Fact]
    public async Task LoginPost_UnregisteredReturnUrl_IsBlockedBeforeCredentialsAreChecked()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp();
        var user = ctx.AddUser();

        var result = await ctx.Controller.Login(user.Email!, "Password1!", "https://evil.example.com/steal");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("UnapprovedApp", view.ViewName);
        Assert.IsNotType<RedirectResult>(result);

        ctx.UserManager.Verify(m => m.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        Assert.Contains(ctx.Db.AuditLogs, a => a.Action == "UnapprovedReturnUrl");
    }
}