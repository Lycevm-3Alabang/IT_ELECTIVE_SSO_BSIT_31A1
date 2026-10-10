using System.Security.Claims;
using Gateway.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Models.Entities;
using Moq;
using Xunit;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Data.Tests;

public class PortalAndPasswordTests
{
    // Makes the controller think this user is signed in (optionally with the Gateway Admin role).
    private static void SignInAs(AuthTestContext ctx, ApplicationUser user, bool admin)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Email!)
        };
        if (admin) claims.Add(new Claim(ClaimTypes.Role, SeedData.AdminRole));

        ctx.Controller.ControllerContext.HttpContext.User =
            new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        ctx.UserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
    }

    // ------------------------------------------------------------------ Portal routing

    [Fact]
    public async Task Portal_Admin_ShowsChooserWithAssignedApps()
    {
        var ctx = new AuthTestContext();
        var app = ctx.AddApp();
        var user = ctx.AddUser();
        ctx.AddGroupForUser(app, user, "SalesApp-Admin", 0);
        SignInAs(ctx, user, admin: true);

        var result = await ctx.Controller.Portal();

        var model = Assert.IsType<PortalViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.IsAdmin);
        Assert.Equal("SalesApp", Assert.Single(model.Apps).Name);
    }

    [Fact]
    public async Task Portal_Admin_WithNoApps_StillGetsChooser()
    {
        var ctx = new AuthTestContext();
        var user = ctx.AddUser();
        SignInAs(ctx, user, admin: true);

        var result = await ctx.Controller.Portal();

        var model = Assert.IsType<PortalViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.IsAdmin);
        Assert.Empty(model.Apps);
    }

    [Fact]
    public async Task Portal_RegularUser_WithOneApp_GoesStraightToAppWithToken()
    {
        var ctx = new AuthTestContext();
        var app = ctx.AddApp();
        var user = ctx.AddUser();
        ctx.AddGroupForUser(app, user, "SalesApp-Users", 1);
        SignInAs(ctx, user, admin: false);

        var result = await ctx.Controller.Portal();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith(AuthTestContext.ReturnUrl + "?token=", redirect.Url);
    }

    [Fact]
    public async Task Portal_RegularUser_WithTwoApps_ShowsChooser()
    {
        var ctx = new AuthTestContext();
        var appA = ctx.AddApp("AppA", "https://a.example.com/callback");
        var appB = ctx.AddApp("AppB", "https://b.example.com/callback");
        var user = ctx.AddUser();
        ctx.AddGroupForUser(appA, user, "AppA-Users", 1);
        ctx.AddGroupForUser(appB, user, "AppB-Users", 1);
        SignInAs(ctx, user, admin: false);

        var result = await ctx.Controller.Portal();

        var model = Assert.IsType<PortalViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.IsAdmin);
        Assert.Equal(2, model.Apps.Count);
    }

    [Fact]
    public async Task Portal_RegularUser_WithNoGroups_SeesEmptyList()
    {
        var ctx = new AuthTestContext();
        var user = ctx.AddUser();
        SignInAs(ctx, user, admin: false);

        var result = await ctx.Controller.Portal();

        var model = Assert.IsType<PortalViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.IsAdmin);
        Assert.Empty(model.Apps);
    }

    [Fact]
    public async Task Portal_IgnoresDisabledApps()
    {
        var ctx = new AuthTestContext();
        var app = ctx.AddApp();
        app.IsActive = false;
        ctx.Db.SaveChanges();
        var user = ctx.AddUser();
        ctx.AddGroupForUser(app, user, "SalesApp-Users", 1);
        SignInAs(ctx, user, admin: false);

        var result = await ctx.Controller.Portal();

        var model = Assert.IsType<PortalViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.Apps);
    }

    [Fact]
    public async Task Launch_AppTheUserIsNotAssignedTo_GoesBackToPortal()
    {
        var ctx = new AuthTestContext();
        var mine = ctx.AddApp("Mine", "https://mine.example.com/callback");
        var other = ctx.AddApp("Other", "https://other.example.com/callback");
        var user = ctx.AddUser();
        ctx.AddGroupForUser(mine, user, "Mine-Users", 1);
        SignInAs(ctx, user, admin: false);

        var result = await ctx.Controller.Launch(other.Id);

        Assert.Equal("Portal", Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    // ------------------------------------------------------ temporary password at login

    [Fact]
    public async Task Login_WithTemporaryPassword_GoesToChangePassword_NotToTheApp()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp();
        var user = ctx.AddUser();
        user.MustChangePassword = true;
        ctx.SignInManager
            .Setup(m => m.CheckPasswordSignInAsync(user, "Temp@12345", true))
            .ReturnsAsync(IdentitySignInResult.Success);

        var result = await ctx.Controller.Login(user.Email!, "Temp@12345", AuthTestContext.ReturnUrl);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ChangePassword", redirect.ActionName);
        Assert.Equal(AuthTestContext.ReturnUrl, redirect.RouteValues!["returnUrl"]);
    }

    // ------------------------------------------------------------------ ChangePassword

    [Fact]
    public async Task ChangePassword_Valid_ClearsFlagAndGoesToPortal()
    {
        var ctx = new AuthTestContext();
        var user = ctx.AddUser();
        user.MustChangePassword = true;
        SignInAs(ctx, user, admin: false);
        ctx.UserManager
            .Setup(m => m.ChangePasswordAsync(user, "Temp@12345", "New@123456"))
            .ReturnsAsync(IdentityResult.Success);

        var result = await ctx.Controller.ChangePassword("Temp@12345", "New@123456", "New@123456", null);

        Assert.Equal("Portal", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.False(user.MustChangePassword);
        ctx.UserManager.Verify(m => m.UpdateAsync(user), Times.Once);
        ctx.SignInManager.Verify(m => m.RefreshSignInAsync(user), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_ValidWithReturnUrl_GoesBackToAppWithToken()
    {
        var ctx = new AuthTestContext();
        ctx.AddApp();
        var user = ctx.AddUser();
        user.MustChangePassword = true;
        SignInAs(ctx, user, admin: false);
        ctx.UserManager
            .Setup(m => m.ChangePasswordAsync(user, "Temp@12345", "New@123456"))
            .ReturnsAsync(IdentityResult.Success);

        var result = await ctx.Controller.ChangePassword(
            "Temp@12345", "New@123456", "New@123456", AuthTestContext.ReturnUrl);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith(AuthTestContext.ReturnUrl + "?token=", redirect.Url);
    }

    [Fact]
    public async Task ChangePassword_ConfirmationMismatch_IsRejectedWithoutChangingAnything()
    {
        var ctx = new AuthTestContext();
        var user = ctx.AddUser();
        user.MustChangePassword = true;
        SignInAs(ctx, user, admin: false);

        var result = await ctx.Controller.ChangePassword("Temp@12345", "New@123456", "Different@1", null);

        Assert.IsType<ViewResult>(result);
        Assert.True(user.MustChangePassword);
        ctx.UserManager.Verify(
            m => m.ChangePasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePassword_SameAsTemporaryPassword_IsRejected()
    {
        var ctx = new AuthTestContext();
        var user = ctx.AddUser();
        user.MustChangePassword = true;
        SignInAs(ctx, user, admin: false);

        var result = await ctx.Controller.ChangePassword("Temp@12345", "Temp@12345", "Temp@12345", null);

        Assert.IsType<ViewResult>(result);
        Assert.True(user.MustChangePassword);
        Assert.False(ctx.Controller.ModelState.IsValid);
    }

    [Fact]
    public async Task ChangePassword_IdentityRejectsPassword_ShowsErrorAndKeepsFlag()
    {
        var ctx = new AuthTestContext();
        var user = ctx.AddUser();
        user.MustChangePassword = true;
        SignInAs(ctx, user, admin: false);
        ctx.UserManager
            .Setup(m => m.ChangePasswordAsync(user, "Temp@12345", "weak"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak." }));

        var result = await ctx.Controller.ChangePassword("Temp@12345", "weak", "weak", null);

        Assert.IsType<ViewResult>(result);
        Assert.True(user.MustChangePassword);
        Assert.False(ctx.Controller.ModelState.IsValid);
    }
}