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

        // TEMPORARY - demo app. Remove once apps are registered through the Admin UI.
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

        // TEMPORARY - registers the MVC client app so the Gateway accepts its returnUrl.
        const string mvcAppName = "MvcClientApp";                        // must match MVC appsettings: Sso:AppName
        const string mvcCallbackUrl = "https://localhost:7180/callback"; // Sso:ClientBaseUrl + /callback

        var mvcApp = await context.Tenants.FirstOrDefaultAsync(t => t.Name == mvcAppName);
        if (mvcApp == null)
        {
            mvcApp = new TenantApp { Name = mvcAppName, ReturnUrl = mvcCallbackUrl, IsActive = true };
            context.Tenants.Add(mvcApp);
        }
        else
        {
            mvcApp.ReturnUrl = mvcCallbackUrl;
            mvcApp.IsActive = true;
        }
        await context.SaveChangesAsync();

        // Level 0 = highest power -> "Admin" role inside MVC. Any other level -> "User".
        var mvcUsersGroup = await EnsureGroupAsync(context, mvcApp.Id, "MvcClientApp-Users", 1);
        var mvcAdminGroup = await EnsureGroupAsync(context, mvcApp.Id, "MvcClientApp-Admin", 0);

        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            var adminForMvc = await userManager.FindByEmailAsync(adminEmail);
            if (adminForMvc != null)
            {
                foreach (var group in new[] { mvcUsersGroup, mvcAdminGroup })
                {
                    var assigned = await context.UserGroups
                        .AnyAsync(ug => ug.UserId == adminForMvc.Id && ug.GroupId == group.Id);
                    if (!assigned)
                    {
                        context.UserGroups.Add(new UserGroup { UserId = adminForMvc.Id, GroupId = group.Id });
                    }
                }
                await context.SaveChangesAsync();
            }
        }
    }

    private static async Task<Group> EnsureGroupAsync(SsoDbContext context, int tenantAppId, string name, int level)
    {
        var group = await context.Groups
            .FirstOrDefaultAsync(g => g.TenantAppId == tenantAppId && g.Name == name);

        if (group == null)
        {
            group = new Group { Name = name, Level = level, TenantAppId = tenantAppId };
            context.Groups.Add(group);
            await context.SaveChangesAsync();
        }
        return group;
    }
}