// Tests/AuthLoginInvalidCredentialsTests.cs
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Data.Tests;

public class AuthLoginInvalidCredentialsTests
{
    [Fact]
    public async Task Login_WrongPassword_ReturnsViewWithErrorAndNoToken()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp();
        var user = ctx.AddUser();

        ctx.SignInManager
            .Setup(m => m.CheckPasswordSignInAsync(user, "wrong", true))
            .ReturnsAsync(IdentitySignInResult.Failed);

        var result = await ctx.Controller.Login(user.Email!, "wrong", AuthTestContext.ReturnUrl);

        Assert.IsType<ViewResult>(result);
        Assert.False(ctx.Controller.ModelState.IsValid);
        Assert.Contains(
            ctx.Controller.ModelState.Values.SelectMany(v => v.Errors),
            e => e.ErrorMessage == "Invalid email or password.");
        Assert.Contains(ctx.Db.AuditLogs, a => a.Action == "LoginFailed");
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsViewWithGenericError()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp();

        var result = await ctx.Controller.Login("nobody@example.com", "whatever", AuthTestContext.ReturnUrl);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            ctx.Controller.ModelState.Values.SelectMany(v => v.Errors),
            e => e.ErrorMessage == "Invalid email or password.");
    }
}