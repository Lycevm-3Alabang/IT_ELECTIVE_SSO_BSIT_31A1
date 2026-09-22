using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Models.Entities;

namespace Data;

public static class SeedData
{
    public const string AdminRole = "Admin";

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var context = serviceProvider.GetRequiredService<SsoDbContext>();

        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRole));
        }

        string? adminEmail = configuration["DefaultAdmin:Email"];
        string? adminPassword = configuration["DefaultAdmin:Password"];

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    IsActive = true
                };
                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, AdminRole);
                }
            }
            else if (!await userManager.IsInRoleAsync(existingAdmin, AdminRole))
            {
                await userManager.AddToRoleAsync(existingAdmin, AdminRole);
            }
        }

        // TEMPORARY — for JWT/login demo purposes only.
        // Remove once Issue 8's admin UI (Tenant App registration) is built,
        // so apps get registered through the actual admin screen instead.
        const string testAppReturnUrl = "https://localhost:7281/Home/Privacy";

        var testApp = await context.Tenants.FirstOrDefaultAsync(t => t.Name == "TestApp");
        if (testApp == null)
        {
            context.Tenants.Add(new TenantApp
            {
                Name = "TestApp",
                ReturnUrl = testAppReturnUrl,
                IsActive = true
            });
        }
        else if (testApp.ReturnUrl != testAppReturnUrl)
        {
            testApp.ReturnUrl = testAppReturnUrl;
        }
        await context.SaveChangesAsync();
    }
}