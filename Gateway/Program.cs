using Data;
using Gateway.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ReturnUrlValidator>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddControllersWithViews();

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