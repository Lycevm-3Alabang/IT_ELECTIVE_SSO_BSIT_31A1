using Microsoft.AspNetCore.Http;

namespace MvcClientApp.Services;

// CSRF defence for actions that must only be triggered from this app's own pages (e.g. logout).
// It does not depend on an antiforgery token/cookie pair, so a stale tab or a restart cannot break it.
public static class RequestOriginGuard
{
    public static bool IsSameOrigin(HttpRequest request)
    {
        // 1) Fetch Metadata: every modern browser sends this header and page scripts cannot change it.
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        if (!string.IsNullOrEmpty(fetchSite))
        {
            return fetchSite.Equals("same-origin", StringComparison.OrdinalIgnoreCase)
                || fetchSite.Equals("none", StringComparison.OrdinalIgnoreCase);   // "none" = user-initiated
        }

        // 2) Older browsers: the Origin header must be exactly this app's own origin.
        var origin = request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin))
        {
            var self = $"{request.Scheme}://{request.Host}";
            return string.Equals(origin, self, StringComparison.OrdinalIgnoreCase);
        }

        // 3) Neither header: not a cross-site browser request (curl, scripts, very old browsers).
        return true;
    }
}