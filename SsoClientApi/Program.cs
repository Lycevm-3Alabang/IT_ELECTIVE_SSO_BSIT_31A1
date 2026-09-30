using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SsoClientApi;

var builder = WebApplication.CreateBuilder(args);

// 1. Read the "Sso" section of appsettings.json
var sso = builder.Configuration.GetSection("Sso").Get<SsoOptions>()
          ?? throw new InvalidOperationException("Missing 'Sso' section in appsettings.json.");
if (string.IsNullOrWhiteSpace(sso.SecretKey))
    throw new InvalidOperationException("Sso:SecretKey must match the Gateway's JwtSettings:SecretKey.");
builder.Services.AddSingleton(sso);

// 2. JWT validation middleware
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep claim names exactly as the Gateway wrote them: sub, email, groups...
        options.TokenValidationParameters = sso.CreateValidationParameters();

        options.Events = new JwtBearerEvents
        {
            // Accept the token from "Authorization: Bearer ..." OR from the session cookie set by /callback
            OnMessageReceived = ctx =>
            {
                if (!ctx.Request.Headers.ContainsKey("Authorization") &&
                    ctx.Request.Cookies.TryGetValue(SsoOptions.CookieName, out var cookieToken))
                {
                    ctx.Token = cookieToken;
                }
                return Task.CompletedTask;
            },

            // A valid token that was issued for a DIFFERENT tenant app must not work here
            OnTokenValidated = ctx =>
            {
                if (!SsoTokenRules.IsIssuedForApp(ctx.Principal, sso.AppName))
                    ctx.Fail("This token was issued for a different app.");
                return Task.CompletedTask;
            },

            // Expired token: remember it (so the home page can say so), tell API clients, drop the stale cookie
            OnAuthenticationFailed = ctx =>
            {
                if (ctx.Exception is SecurityTokenExpiredException)
                {
                    ctx.HttpContext.Items[SsoOptions.ExpiredFlag] = true;
                    ctx.Response.Headers["Token-Expired"] = "true";
                    ctx.Response.Cookies.Delete(SsoOptions.CookieName);
                }
                return Task.CompletedTask;
            },

            // Not signed in / bad token: JSON 401 for /api/*, friendly redirect for browser pages
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse(); // we write the response ourselves
                var expired = ctx.HttpContext.Items.ContainsKey(SsoOptions.ExpiredFlag)
                              || ctx.AuthenticateFailure is SecurityTokenExpiredException;

                if (ctx.Request.Path.StartsWithSegments("/api"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsJsonAsync(new ApiError(
                        expired ? "token_expired" : "unauthorized",
                        expired ? "Your SSO session has expired. Please log in again."
                                : "A valid SSO token is required.",
                        "/login"));
                }
                else
                {
                    ctx.Response.Redirect(expired ? "/?error=expired" : "/login");
                }
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Home page: shows the "Login with SSO" button (or who you are, once signed in)
app.MapGet("/", (HttpContext ctx) =>
{
    var expired = ctx.Items.ContainsKey(SsoOptions.ExpiredFlag);
    var error = ctx.Request.Query["error"].ToString();
    return Results.Content(HomePage.Render(ctx.User, error, expired), "text/html");
});

// "Login with SSO" -> send the browser to the Gateway; it comes back to /callback?token=...
app.MapGet("/login", (SsoOptions opts) => Results.Redirect(opts.GatewayLoginUrl));

// The Gateway redirects here after a successful login
app.MapGet("/callback", async (string? token, HttpContext ctx, SsoOptions opts) =>
{
    if (string.IsNullOrWhiteSpace(token))
        return Results.Redirect("/?error=missing_token");

    var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, opts.CreateValidationParameters());
    if (!result.IsValid)
    {
        return Results.Redirect("/?error=invalid_token");
    }

    if (!SsoTokenRules.IsIssuedForApp(new ClaimsPrincipal(result.ClaimsIdentity), opts.AppName))
        return Results.Redirect("/?error=wrong_app");

    // Keep the token in an HttpOnly cookie that expires together with the JWT
    var jwt = (JsonWebToken)result.SecurityToken;
    ctx.Response.Cookies.Append(SsoOptions.CookieName, token, new CookieOptions
    {
        HttpOnly = true,
        Secure = ctx.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Expires = new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero)
    });

    // Redirect so the token disappears from the address bar and browser history
    return Results.Redirect("/");
});

// Local logout only (clears THIS app's cookie; the Gateway session is separate)
app.MapGet("/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete(SsoOptions.CookieName);
    return Results.Redirect("/");
});

// GET /api/userinfo : needs a valid token (Bearer header or session cookie)
app.MapGet("/api/userinfo", (ClaimsPrincipal user) => Results.Ok(UserInfoMapper.FromPrincipal(user)))
   .RequireAuthorization();

app.Run();