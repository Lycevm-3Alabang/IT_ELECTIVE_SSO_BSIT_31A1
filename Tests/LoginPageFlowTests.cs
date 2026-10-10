// Tests/LoginPageFlowTests.cs
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Data.Tests;

public class LoginPageFlowTests
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

    [Fact]
    public async Task Get_WithRegisteredReturnUrl_ShowsLoginFormWithAppName()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp("SalesApp");

        var result = await ctx.Controller.Login(AuthTestContext.ReturnUrl);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        Assert.Equal("SalesApp", (string)ctx.Controller.ViewBag.AppName);
        Assert.Equal(AuthTestContext.ReturnUrl, (string)ctx.Controller.ViewBag.ReturnUrl);
    }

    [Fact]
    public async Task Get_WithoutReturnUrl_ShowsGenericLoginForm()
    {
        var ctx = new AuthTestContext();

        var result = await ctx.Controller.Login(null);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        Assert.Null((string?)ctx.Controller.ViewBag.AppName);
    }

    [Fact]
    public async Task Post_ValidLoginWithoutReturnUrl_RedirectsToPortal()
    {
        var ctx = new AuthTestContext();
        var user = ctx.AddUser();

        ctx.SignInManager
            .Setup(m => m.CheckPasswordSignInAsync(user, "Password1!", true))
            .ReturnsAsync(IdentitySignInResult.Success);

        var result = await ctx.Controller.Login(user.Email!, "Password1!", null);

        // No client app involved -> Portal decides: admins get the chooser,
        // regular users go straight to their app.
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Portal", redirect.ActionName);
        Assert.Null(redirect.ControllerName);   // same controller (Auth)
    }

    [Fact]
    public async Task Post_WrongPassword_KeepsAppNameAndReturnUrlOnTheForm()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp("SalesApp");
        var user = ctx.AddUser();
        ctx.SignInManager
            .Setup(m => m.CheckPasswordSignInAsync(user, "wrong", true))
            .ReturnsAsync(IdentitySignInResult.Failed);

        await ctx.Controller.Login(user.Email!, "wrong", AuthTestContext.ReturnUrl);

        Assert.Equal("SalesApp", (string)ctx.Controller.ViewBag.AppName);
        Assert.Equal(AuthTestContext.ReturnUrl, (string)ctx.Controller.ViewBag.ReturnUrl);
    }

    [Fact]
    public void LoginView_HasSsoTitleAndCredentialFields()
    {
        var html = ReadGatewayFile("Views/Auth/Login.cshtml");

        Assert.Contains("IT ELECTIVE - SSO", html);
        Assert.Contains("name=\"email\"", html);
        Assert.Contains("name=\"password\"", html);
        Assert.Contains("name=\"returnUrl\"", html);
        Assert.Contains("type=\"submit\"", html);
    }

    [Fact]
    public void LoginView_HasNoRegistrationOrDeadLinks()
    {
        var html = ReadGatewayFile("Views/Auth/Login.cshtml");

        Assert.DoesNotContain("register", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sign up", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"#\"", html);
    }
}