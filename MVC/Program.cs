using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MvcClientApp.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Basahin ang "Sso" section ng appsettings.json
var sso = builder.Configuration.GetSection("Sso").Get<SsoOptions>()
          ?? throw new InvalidOperationException("Missing 'Sso' section in appsettings.json.");
if (string.IsNullOrWhiteSpace(sso.SecretKey))
    throw new InvalidOperationException("Sso:SecretKey must match the Gateway's JwtSettings:SecretKey.");
builder.Services.AddSingleton(sso);

builder.Services.AddControllersWithViews();

// 2. JWT validation (galing sa cookie na ginawa ng /callback)
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = sso.CreateValidationParameters();

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (!ctx.Request.Headers.ContainsKey("Authorization") &&
                    ctx.Request.Cookies.TryGetValue(SsoOptions.CookieName, out var cookieToken))
                {
                    ctx.Token = cookieToken;
                }
                return Task.CompletedTask;
            },

            // Hindi pwede ang token na para sa ibang tenant app,
            // at kailangang may group ang user para sa app na ito
            OnTokenValidated = ctx =>
            {
                if (!SsoTokenRules.IsIssuedForApp(ctx.Principal, sso.AppName))
                    ctx.Fail("This token was issued for a different app.");
                else if (!SsoTokenRules.HasAppAccess(ctx.Principal))
                    ctx.Fail("This user is not assigned to this app.");
                return Task.CompletedTask;
            },

            OnAuthenticationFailed = ctx =>
            {
                if (ctx.Exception is SecurityTokenExpiredException)
                {
                    ctx.HttpContext.Items[SsoOptions.ExpiredFlag] = true;
                    ctx.Response.Cookies.Delete(SsoOptions.CookieName);
                }
                return Task.CompletedTask;
            },

            // Hindi naka-login sa protected page -> papunta sa SSO login
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                var expired = ctx.HttpContext.Items.ContainsKey(SsoOptions.ExpiredFlag)
                              || ctx.AuthenticateFailure is SecurityTokenExpiredException;
                ctx.Response.Redirect(expired ? "/?error=expired" : "/login");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();