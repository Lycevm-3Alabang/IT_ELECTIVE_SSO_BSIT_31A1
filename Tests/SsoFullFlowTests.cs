using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SsoClientApi;
using Xunit;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Data.Tests;

// Full SSO flow:  Gateway login (real AuthController + JwtTokenService)
//              -> JWT in the redirect
//              -> mock client (SsoClientApi, hosted in memory) validates it and serves /api/userinfo.
// SsoOptions is only used to point WebApplicationFactory at the SsoClientApi assembly
// (Gateway and SsoClientApi both have a "Program" class, so we avoid naming it).
public class SsoFullFlowTests : IClassFixture<WebApplicationFactory<SsoOptions>>
{
    private const string ClientApp = "MockClientApp";
    private const string ClientCallback = "https://localhost:7080/callback";
    private const string Secret = "Your_Super_Secret_Key_Here_Make_It_Long"; // = JwtSettings:SecretKey = Sso:SecretKey

    private readonly WebApplicationFactory<SsoOptions> _factory;

    public SsoFullFlowTests(WebApplicationFactory<SsoOptions> factory) => _factory = factory;

    // ---- helpers ---------------------------------------------------------------------------

    /// <summary>Gateway half of the flow: log in and return the JWT from the redirect URL.</summary>
    private static async Task<string> LoginAtGatewayAsync()
    {
        var ctx = new AuthTestContext();
        var app = ctx.AddApp(ClientApp, ClientCallback);
        var user = ctx.AddUser();
        ctx.AddGroupForUser(app, user, "MockClientApp-Admin", 0);
        ctx.AddGroupForUser(app, user, "MockClientApp-Viewer", 2);

        ctx.SignInManager
            .Setup(m => m.CheckPasswordSignInAsync(user, "Password1!", true))
            .ReturnsAsync(IdentitySignInResult.Success);

        var result = await ctx.Controller.Login(user.Email!, "Password1!", ClientCallback);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith(ClientCallback + "?token=", redirect.Url);
        return redirect.Url.Split("?token=")[1];
    }

    /// <summary>Hand-made token for the failure cases (wrong app, expired, wrong key).</summary>
    private static string MakeToken(string tenantApp, DateTime expiresUtc, string secret = Secret)
    {
        var claims = new[]
        {
            new Claim("sub", "u1"),
            new Claim("email", "user@example.com"),
            new Claim("tenant_app", tenantApp),
            new Claim("groups", $"{tenantApp}-Admin"),
            new Claim("levels", $"{{\"{tenantApp}-Admin\":0}}")
        };
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken("SSOGateway", "SSOClientApps", claims,
            notBefore: expiresUtc.AddHours(-9), expires: expiresUtc, signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // ---- tests -----------------------------------------------------------------------------

    [Fact]
    public async Task FullFlow_GatewayLogin_ThenCallback_ThenUserInfo()
    {
        var token = await LoginAtGatewayAsync();
        var client = NewClient();

        // 1. Browser follows the Gateway's redirect to the client's /callback?token=...
        var callback = await client.GetAsync($"/callback?token={token}");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);
        Assert.Contains(callback.Headers.GetValues("Set-Cookie"), c => c.StartsWith("sso_token="));

        // 2. The session cookie is now stored, so /api/userinfo works without an Authorization header
        var response = await client.GetAsync("/api/userinfo");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var info = await response.Content.ReadFromJsonAsync<UserInfoResponse>();
        Assert.NotNull(info);
        Assert.Equal("u1", info!.Sub);
        Assert.Equal("user@example.com", info.Email);
        Assert.Equal(ClientApp, info.TenantApp);
        Assert.Equal(
            new[] { "MockClientApp-Admin", "MockClientApp-Viewer" },
            info.Groups.OrderBy(g => g).ToArray());
        Assert.Equal(0, info.Levels["MockClientApp-Admin"]);
        Assert.Equal(2, info.Levels["MockClientApp-Viewer"]);
        Assert.True(info.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task UserInfo_AcceptsBearerToken()
    {
        var token = await LoginAtGatewayAsync();
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/userinfo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserInfo_WithoutToken_Returns401Json()
    {
        var response = await NewClient().GetAsync("/api/userinfo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("unauthorized", error!.Error);
    }

    [Fact]
    public async Task UserInfo_ExpiredToken_Returns401TokenExpired()
    {
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MakeToken(ClientApp, DateTime.UtcNow.AddHours(-1)));

        var response = await client.GetAsync("/api/userinfo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains("Token-Expired"));
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("token_expired", error!.Error);
    }

    [Fact]
    public async Task Callback_ExpiredToken_RedirectsHomeWithExpiredMessage()
    {
        var token = MakeToken(ClientApp, DateTime.UtcNow.AddHours(-1));

        var response = await NewClient().GetAsync($"/callback?token={token}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/?error=expired", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Callback_TokenForAnotherApp_IsRejected()
    {
        var token = MakeToken("SomeOtherApp", DateTime.UtcNow.AddHours(9));

        var response = await NewClient().GetAsync($"/callback?token={token}");

        Assert.Equal("/?error=wrong_app", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Callback_TokenSignedWithWrongKey_IsRejected()
    {
        var token = MakeToken(ClientApp, DateTime.UtcNow.AddHours(9), secret: "a_completely_different_secret_key_123456");

        var response = await NewClient().GetAsync($"/callback?token={token}");

        Assert.Equal("/?error=invalid_token", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task HomePage_ShowsLoginWithSsoButton()
    {
        var html = await NewClient().GetStringAsync("/");

        Assert.Contains("Login with SSO", html);
    }
}