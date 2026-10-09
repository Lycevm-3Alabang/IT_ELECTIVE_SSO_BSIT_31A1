using Microsoft.AspNetCore.Http;
using MvcClientApp.Services;
using Xunit;

namespace Data.Tests;

public class RequestOriginGuardTests
{
    private static HttpRequest NewRequest(string? fetchSite = null, string? origin = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("localhost", 7180);
        if (fetchSite != null) ctx.Request.Headers["Sec-Fetch-Site"] = fetchSite;
        if (origin != null) ctx.Request.Headers["Origin"] = origin;
        return ctx.Request;
    }

    [Fact]
    public void SameOriginFetchSite_IsAllowed() =>
        Assert.True(RequestOriginGuard.IsSameOrigin(NewRequest(fetchSite: "same-origin")));

    [Theory]
    [InlineData("cross-site")]
    [InlineData("same-site")]   // e.g. the Gateway on another port: same site, different origin
    public void OtherFetchSites_AreBlocked(string fetchSite) =>
        Assert.False(RequestOriginGuard.IsSameOrigin(NewRequest(fetchSite: fetchSite)));

    [Fact]
    public void OriginMatchingThisApp_IsAllowed_WhenNoFetchMetadata() =>
        Assert.True(RequestOriginGuard.IsSameOrigin(NewRequest(origin: "https://localhost:7180")));

    [Fact]
    public void OriginFromAnotherSite_IsBlocked_WhenNoFetchMetadata() =>
        Assert.False(RequestOriginGuard.IsSameOrigin(NewRequest(origin: "https://evil.example.com")));

    [Fact]
    public void RequestWithoutAnyHeaders_IsAllowed() =>
        Assert.True(RequestOriginGuard.IsSameOrigin(NewRequest()));
}