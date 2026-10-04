using System.Security.Claims;
using MvcClientApp.Services;
using Xunit;

namespace Data.Tests;

public class SsoTokenRulesTests
{
    [Fact]
    public void LevelZero_GivesAdminAndUser()
    {
        var roles = SsoTokenRules.RolesFromLevels("{\"MvcClientApp-Admin\":0,\"MvcClientApp-Users\":1}");

        Assert.Contains("Admin", roles);
        Assert.Contains("User", roles);
    }

    [Fact]
    public void OnlyLowerLevels_GivesUserOnly()
    {
        var roles = SsoTokenRules.RolesFromLevels("{\"MvcClientApp-Users\":1,\"MvcClientApp-Viewer\":2}");

        Assert.Equal(new[] { "User" }, roles);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("this is not json")]
    public void MissingOrBrokenLevels_FallBackToUserOnly(string? levels)
    {
        Assert.Equal(new[] { "User" }, SsoTokenRules.RolesFromLevels(levels));
    }

    [Fact]
    public void SessionIdentity_CarriesRolesAndUserClaims()
    {
        var jwtIdentity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "u1"),
            new Claim("email", "teacher@test.com"),
            new Claim("tenant_app", "MvcClientApp"),
            new Claim("groups", "MvcClientApp-Admin"),
            new Claim("levels", "{\"MvcClientApp-Admin\":0}")
        }, "jwt");

        var identity = SsoTokenRules.BuildSessionIdentity(jwtIdentity, DateTimeOffset.UtcNow.AddHours(9));
        var principal = new ClaimsPrincipal(identity);

        Assert.True(principal.IsInRole("Admin"));
        Assert.True(principal.IsInRole("User"));
        Assert.Equal("teacher@test.com", principal.Identity!.Name);
        Assert.Equal("MvcClientApp-Admin", principal.FindFirst("groups")!.Value);
    }

    [Fact]
    public void SessionIdentity_ForPlainUser_HasNoAdminRole()
    {
        var jwtIdentity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "u2"),
            new Claim("email", "student@test.com"),
            new Claim("tenant_app", "MvcClientApp"),
            new Claim("groups", "MvcClientApp-Users"),
            new Claim("levels", "{\"MvcClientApp-Users\":1}")
        }, "jwt");

        var principal = new ClaimsPrincipal(
            SsoTokenRules.BuildSessionIdentity(jwtIdentity, DateTimeOffset.UtcNow.AddHours(9)));

        Assert.True(principal.IsInRole("User"));
        Assert.False(principal.IsInRole("Admin"));
    }

    [Fact]
    public void TokenForAnotherApp_IsRejected()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenant_app", "SomeOtherApp") }, "jwt"));

        Assert.False(SsoTokenRules.IsIssuedForApp(principal, "MvcClientApp"));
    }

    [Fact]
    public void TokenWithoutGroups_HasNoAppAccess()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("groups", "") }, "jwt"));

        Assert.False(SsoTokenRules.HasAppAccess(principal));
    }
}