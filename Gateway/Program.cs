// Gateway/Program.cs (COMPLETE FILE)
using System.Text;
using Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Models.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ReturnUrlValidator>();
builder.Services.AddScoped<Gateway.Services.JwtTokenService>();
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<SsoDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<SsoDbContext>()
    .AddDefaultTokenProviders();

// Configure JWT Bearer Authentication alongside ASP.NET Core Identity
builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"], // e.g., "https://localhost:5001" (SSO Gateway)
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/";
    options.AccessDeniedPath = "/";
});

// CORS: allow the registered external client apps to call the SSO gateway
// (e.g. the mock client app's /api/userinfo calling back, or any AJAX
// login flow) from a different origin.
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

// Configure the HTTP request pipeline.
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

// Area route must come BEFORE the default route, or "/Admin/..." requests
// get matched by the default route instead (and 404, since there's no
// "AdminController").
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
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

app.Run();