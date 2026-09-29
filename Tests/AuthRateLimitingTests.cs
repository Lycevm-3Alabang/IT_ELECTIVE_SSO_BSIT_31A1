// Tests/AuthRateLimitingTests.cs
using Gateway.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Data.Tests;

public class AuthRateLimitingTests
{
    [Fact]
    public void LockoutConfig_Is5Attempts_For15Minutes()
    {
        var options = new IdentityOptions();

        AuthSecurityOptions.ApplyLockout(options.Lockout);

        Assert.True(options.Lockout.AllowedForNewUsers);
        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Lockout.DefaultLockoutTimeSpan);
    }

    [Fact]
    public async Task Login_LockedOutUser_IsRejectedAndLogged()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp();
        var user = ctx.AddUser();

        ctx.SignInManager
            .Setup(m => m.CheckPasswordSignInAsync(user, "Password1!", true))
            .ReturnsAsync(IdentitySignInResult.LockedOut);

        var result = await ctx.Controller.Login(user.Email!, "Password1!", AuthTestContext.ReturnUrl);

        Assert.IsType<ViewResult>(result);
        Assert.IsNotType<RedirectResult>(result);
        Assert.Contains(
            ctx.Controller.ModelState.Values.SelectMany(v => v.Errors),
            e => e.ErrorMessage.Contains("Too many failed attempts"));
        Assert.Contains(ctx.Db.AuditLogs,
            a => a.Action == "LoginFailed" && a.Details!.Contains("locked out"));
    }
}