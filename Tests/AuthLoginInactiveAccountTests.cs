// Tests/AuthLoginInactiveAccountTests.cs
using Microsoft.AspNetCore.Mvc;
using Models.Entities;
using Moq;
using Xunit;

namespace Data.Tests;

public class AuthLoginInactiveAccountTests
{
    [Fact]
    public async Task Login_InactiveUser_IsRejectedWithSuspendedMessage()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp();
        var user = ctx.AddUser(isActive: false);

        var result = await ctx.Controller.Login(user.Email!, "Password1!", AuthTestContext.ReturnUrl);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            ctx.Controller.ModelState.Values.SelectMany(v => v.Errors),
            e => e.ErrorMessage.Contains("Account Suspended"));

        // Password must never be checked, and no token issued
        ctx.SignInManager.Verify(
            m => m.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
        Assert.Contains(ctx.Db.AuditLogs, a => a.Action == "LoginFailed");
    }
}