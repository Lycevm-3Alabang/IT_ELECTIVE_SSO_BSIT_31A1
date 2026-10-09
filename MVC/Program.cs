using Microsoft.AspNetCore.Authentication.Cookies;
using MvcClientApp.Services;

var builder = WebApplication.CreateBuilder(args);

var sso = builder.Configuration.GetSection("Sso").Get<SsoOptions>()
          ?? throw new InvalidOperationException("Missing 'Sso' section in appsettings.json.");

if (string.IsNullOrWhiteSpace(sso.SecretKey))
    throw new InvalidOperationException("Sso:SecretKey must match the Gateway's JwtSettings:SecretKey.");

builder.Services.AddSingleton(sso);
builder.Services.AddControllersWithViews();

// Antiforgery cookie gets its own name, for the same reason as the session cookie:
// localhost cookies are shared by every port, so Gateway and MVC must not share a name.
builder.Services.AddAntiforgery(options => options.Cookie.Name = "mvc.antiforgery");

// TEMPORARY: print why a token check fails (remove once logout works).
builder.Logging.AddFilter("Microsoft.AspNetCore.Antiforgery", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc.ViewFeatures", LogLevel.Information);

// The JWT is validated once in AccountController.Callback.
// After that, this cookie IS the session (it expires together with the JWT).
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "mvc.session";          // distinct name: localhost cookies are shared across ports
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/login";                 // -> redirects to the Gateway login
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.SlidingExpiration = false;            // never outlive the JWT
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