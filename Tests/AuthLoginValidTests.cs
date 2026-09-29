// Tests/AuthLoginValidTests.cs  (COMPLETE FILE)
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Data.Tests;

public class AuthLoginValidTests
{
    [Fact]
    public async Task Login_ValidCredentials_RedirectsToReturnUrlWithJwt()
    {
        var ctx = new AuthTestContext();
        var app = ctx.AddApp();
        var user = ctx.AddUser();
        ctx.AddGroupForUser(app, user, "SalesApp-Admin", 0);

        ctx.SignInManager
            .Setup(m => m.CheckPasswordSignInAsync(user, "Password1!", true))
            .ReturnsAsync(IdentitySignInResult.Success);

        var result = await ctx.Controller.Login(user.Email!, "Password1!", AuthTestContext.ReturnUrl);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith(AuthTestContext.ReturnUrl + "?token=", redirect.Url);

        var token = redirect.Url.Split("?token=")[1];
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("u1", jwt.Subject);
        Assert.Equal("user@example.com", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.Equal("SalesApp", jwt.Claims.First(c => c.Type == "tenant_app").Value);
        Assert.Equal("SalesApp-Admin", jwt.Claims.First(c => c.Type == "groups").Value);

        var levelsJson = jwt.Claims.First(c => c.Type == "levels").Value;
        var levels = JsonSerializer.Deserialize<Dictionary<string, int>>(levelsJson)!;
        Assert.Equal(0, levels["SalesApp-Admin"]);

        Assert.Contains(jwt.Claims, c => c.Type == "iat");
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }
}