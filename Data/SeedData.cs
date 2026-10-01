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

        // TEMPORARY — registers the MVC client app so the Gateway accepts its returnUrl.
        // Remove once the app is registered through the Admin UI.
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

        // The token only carries groups of THIS app, and the MVC app rejects users with no group.
        const string mvcGroupName = "MvcClientApp-Users"; // same [AppName]-[GroupName] format as the Admin UI
        var mvcGroup = await context.Groups
            .FirstOrDefaultAsync(g => g.TenantAppId == mvcApp.Id && g.Name == mvcGroupName);
        if (mvcGroup == null)
        {
            mvcGroup = new Group { Name = mvcGroupName, Level = 1, TenantAppId = mvcApp.Id };
            context.Groups.Add(mvcGroup);
            await context.SaveChangesAsync();
        }

        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            var adminForMvc = await userManager.FindByEmailAsync(adminEmail);
            if (adminForMvc != null &&
                !await context.UserGroups.AnyAsync(ug => ug.UserId == adminForMvc.Id && ug.GroupId == mvcGroup.Id))
            {
                context.UserGroups.Add(new UserGroup { UserId = adminForMvc.Id, GroupId = mvcGroup.Id });
                await context.SaveChangesAsync();
            }
        }
    }
}