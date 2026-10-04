using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace SsoClientApi;

/// <summary>Tiny server-rendered home page for the mock client (no Razor / MVC needed).</summary>
public static class HomePage
{
    private static readonly Dictionary<string, string> ErrorMessages = new()
    {
        ["expired"] = "Your SSO session has expired. Please log in again.",
        ["invalid_token"] = "The token from the SSO gateway could not be verified.",
        ["missing_token"] = "No token was received from the SSO gateway.",
        ["wrong_app"] = "That token was issued for a different app."
    };

    public static string Render(ClaimsPrincipal user, string? error, bool expired)
    {
        if (expired) error = "expired";

        var banner = "";
        if (!string.IsNullOrEmpty(error) && ErrorMessages.TryGetValue(error, out var message))
            banner = $"<div class=\"alert\" role=\"alert\">{WebUtility.HtmlEncode(message)}</div>";

        var body = user.Identity?.IsAuthenticated == true ? SignedInBody(user) : SignedOutBody();

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Mock Client App | IT ELECTIVE - SSO</title>
              <style>
                body { font-family: system-ui, sans-serif; background: #f4f6f8; margin: 0; }
                main { max-width: 560px; margin: 10vh auto; background: #fff; padding: 2rem; border-radius: 12px; box-shadow: 0 2px 12px rgba(0,0,0,.08); }
                .btn { display: inline-block; padding: .7rem 1.2rem; border: 0; border-radius: 8px; background: #2563eb; color: #fff; font-size: 1rem; text-decoration: none; cursor: pointer; }
                .btn.secondary { background: #64748b; }
                .alert { background: #fee2e2; color: #991b1b; padding: .75rem 1rem; border-radius: 8px; margin-bottom: 1rem; }
                code { background: #f1f5f9; padding: .15rem .4rem; border-radius: 4px; }
              </style>
            </head>
            <body>
              <main>
                <h1>Mock Client App</h1>
                {{banner}}
                {{body}}
              </main>
            </body>
            </html>
            """;
    }

    private static string SignedOutBody() => """
        <p>You are not signed in.</p>
        <form action="/login" method="get">
          <button type="submit" class="btn">Login with SSO</button>
        </form>
        """;

    private static string SignedInBody(ClaimsPrincipal user)
    {
        var info = UserInfoMapper.FromPrincipal(user);

        var groups = info.Groups.Length == 0
            ? "<em>none</em>"
            : string.Join(", ", info.Groups.Select(g => $"<code>{WebUtility.HtmlEncode(g)}</code>"));
        var levels = WebUtility.HtmlEncode(JsonSerializer.Serialize(info.Levels));

        return $"""
            <p>Signed in as <strong>{WebUtility.HtmlEncode(info.Email ?? "(no email)")}</strong></p>
            <p>App: <code>{WebUtility.HtmlEncode(info.TenantApp ?? "")}</code></p>
            <p>Groups: {groups}</p>
            <p>Levels: <code>{levels}</code></p>
            <p>Session expires (UTC): {info.ExpiresAt:u}</p>
            <p>
              <a class="btn" href="/api/userinfo">View /api/userinfo</a>
              <a class="btn secondary" href="/logout">Logout</a>
            </p>
            """;
    }
}