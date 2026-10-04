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

        // TEMPORARY demo apps. They are created ONCE when missing and never overwritten,
        // so anything changed in the Admin UI (disabled, new return URL, deleted groups)
        // survives a restart.
        if (!await context.Tenants.AnyAsync(t => t.Name == "TestApp"))
        {
            context.Tenants.Add(new TenantApp
            {
                Name = "TestApp",
                ReturnUrl = "https://localhost:7281/Home/Privacy",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        const string mvcAppName = "MvcClientApp";                        // must match MVC appsettings: Sso:AppName
        const string mvcCallbackUrl = "https://localhost:7180/callback"; // Sso:ClientBaseUrl + /callback

        var mvcApp = await context.Tenants.FirstOrDefaultAsync(t => t.Name == mvcAppName);
        if (mvcApp != null) return;   // already set up: leave it exactly as the admin left it

        mvcApp = new TenantApp { Name = mvcAppName, ReturnUrl = mvcCallbackUrl, IsActive = true };
        context.Tenants.Add(mvcApp);
        await context.SaveChangesAsync();

        // First run only. Level 0 = highest power -> "Admin" role inside MVC; any other level -> "User".
        var usersGroup = new Group { Name = "MvcClientApp-Users", Level = 1, TenantAppId = mvcApp.Id };
        var adminGroup = new Group { Name = "MvcClientApp-Admin", Level = 0, TenantAppId = mvcApp.Id };
        context.Groups.AddRange(usersGroup, adminGroup);
        await context.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            var adminForMvc = await userManager.FindByEmailAsync(adminEmail);
            if (adminForMvc != null)
            {
                context.UserGroups.Add(new UserGroup { UserId = adminForMvc.Id, GroupId = usersGroup.Id });
                context.UserGroups.Add(new UserGroup { UserId = adminForMvc.Id, GroupId = adminGroup.Id });
                await context.SaveChangesAsync();
            }
        }
    }
}