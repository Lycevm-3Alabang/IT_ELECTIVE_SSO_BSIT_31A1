using Data;
using Gateway.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["JwtSettings:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey is missing or shorter than 32 characters. " +
        "Set it with: dotnet user-secrets set \"JwtSettings:SecretKey\" \"<long random value>\" (see README).");
}

builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ReturnUrlValidator>();
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddControllersWithViews(options =>
{
    // Every POST/PUT/DELETE in the Gateway (Users, Groups, TenantApps, Auth, Account)
    // must carry a valid antiforgery token. GET requests are not affected.
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddAntiforgery(options => options.Cookie.Name = "SSO.Gateway.Antiforgery");

builder.Services.AddDbContext<SsoDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    AuthSecurityOptions.ApplyLockout(options.Lockout);   // 5 attempts / 15 min
    options.User.RequireUniqueEmail = true;              // duplicate emails blocked
})
    .AddEntityFrameworkStores<SsoDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "SSO.Gateway";            // own name: localhost cookies are shared across ports
    options.LoginPath = "/Auth/Login";
    options.AccessDeniedPath = "/Auth/Portal";      // non-admin hitting /Admin -> routed to their app
    options.ReturnUrlParameter = "next";            // do NOT collide with our own "returnUrl"
    options.ExpireTimeSpan = TimeSpan.FromHours(9);
    options.SlidingExpiration = false;
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApps", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors("ClientApps");

app.UseAuthentication();

// Gate: a signed-in user who still has a temporary password can only reach the
// change-password page (and sign-out). Everything else bounces back there.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var path = context.Request.Path;
        var allowed =
            path.StartsWithSegments("/Auth/ChangePassword", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Account/Logout", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Auth/Logout", StringComparison.OrdinalIgnoreCase) ||
            Path.HasExtension(path);   // css / js / lib / favicon

        if (!allowed)
        {
            var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var current = await userManager.GetUserAsync(context.User);
            if (current?.MustChangePassword == true)
            {
                context.Response.Redirect("/Auth/ChangePassword");
                return;
            }
        }
    }
    await next();
});

app.UseAuthorization();

app.MapStaticAssets();

// Area route must come BEFORE the default route.
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SsoDbContext>();
    await db.Database.MigrateAsync();                       // create/upgrade DB first
    await SeedData.InitializeAsync(scope.ServiceProvider);  // then seed
}

app.Run();