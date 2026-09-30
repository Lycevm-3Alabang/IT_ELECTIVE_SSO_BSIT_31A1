// Tests/LoginErrorStatesTests.cs
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Data.Tests;

public class LoginErrorStatesTests
{
    private static string ReadGatewayFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Gateway", relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Gateway/{relativePath} not found above {AppContext.BaseDirectory}");
    }

    private static string ErrorText(AuthTestContext ctx) =>
        string.Join(" | ", ctx.Controller.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage));

    private static void AssertLoginFormRedisplayed(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName); // the Login view, not UnapprovedApp
    }

    [Fact]
    public async Task WrongPassword_ShowsGenericErrorOnLoginForm()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp();
        var user = ctx.AddUser();
        ctx.SignInManager
            .Setup(m => m.CheckPasswordSignInAsync(user, "wrong", true))
            .ReturnsAsync(IdentitySignInResult.Failed);

        var result = await ctx.Controller.Login(user.Email!, "wrong", AuthTestContext.ReturnUrl);

        AssertLoginFormRedisplayed(result);
        Assert.Contains("Invalid email or password.", ErrorText(ctx));
    }

    [Fact]
    public async Task UnknownEmail_ShowsSameMessageAsWrongPassword()
    {
        var wrongPassword = new AuthTestContext();
        wrongPassword.AddApp();
        var user = wrongPassword.AddUser();
        wrongPassword.SignInManager
            .Setup(m => m.CheckPasswordSignInAsync(user, "wrong", true))
            .ReturnsAsync(IdentitySignInResult.Failed);
        await wrongPassword.Controller.Login(user.Email!, "wrong", AuthTestContext.ReturnUrl);

        var unknown = new AuthTestContext();
        unknown.AddApp();
        await unknown.Controller.Login("nobody@example.com", "wrong", AuthTestContext.ReturnUrl);

        // Same text either way, so the page never reveals which emails exist
        Assert.Equal(ErrorText(wrongPassword), ErrorText(unknown));
    }

    [Fact]
    public async Task InactiveAccount_ShowsSuspendedMessage()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp();
        var user = ctx.AddUser(isActive: false);

        var result = await ctx.Controller.Login(user.Email!, "Password1!", AuthTestContext.ReturnUrl);

        AssertLoginFormRedisplayed(result);
        Assert.Contains("Account Suspended", ErrorText(ctx));
    }

    [Fact]
    public async Task LockedOutAccount_ShowsTooManyAttemptsMessage()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp();
        var user = ctx.AddUser();
        ctx.SignInManager
            .Setup(m => m.CheckPasswordSignInAsync(user, "Password1!", true))
            .ReturnsAsync(IdentitySignInResult.LockedOut);

        var result = await ctx.Controller.Login(user.Email!, "Password1!", AuthTestContext.ReturnUrl);

        AssertLoginFormRedisplayed(result);
        Assert.Contains("Too many failed attempts", ErrorText(ctx));
    }

    [Fact]
    public void LoginView_RendersErrorsInsideAlertRegion()
    {
        var html = ReadGatewayFile("Views/Auth/Login.cshtml");

        Assert.Contains("ModelState", html);
        Assert.Contains("role=\"alert\"", html);
    }

    [Fact]
    public void UnapprovedAppView_ShowsClearMessage()
    {
        var html = ReadGatewayFile("Views/Auth/UnapprovedApp.cshtml");

        Assert.Contains("Unapproved External App", html);
        Assert.Contains("role=\"alert\"", html);
    }

    [Fact]
    public void AuthCss_DefinesErrorAlertStyle()
    {
        var css = ReadGatewayFile("wwwroot/css/auth.css");

        Assert.Contains(".auth-alert", css);
    }
}