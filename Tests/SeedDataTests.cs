using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Models.Entities;
using Xunit;

namespace Data.Tests;

public class SeedDataTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager() =>
        new(new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    private static Mock<RoleManager<IdentityRole>> MockRoleManager()
    {
        var store = new Mock<IRoleStore<IdentityRole>>();
        var roleManager = new Mock<RoleManager<IdentityRole>>(store.Object, null!, null!, null!, null!);
        roleManager.Setup(m => m.RoleExistsAsync(SeedData.AdminRole)).ReturnsAsync(true);
        return roleManager;
    }

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DefaultAdmin:Email"] = "admin@example.com",
                ["DefaultAdmin:Password"] = "AdminPassword123!",
            })
            .Build();

    private static IServiceProvider BuildServices(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddSingleton(userManager);
        services.AddSingleton(roleManager);
        services.AddSingleton(config);
        services.AddDbContext<SsoDbContext>(o =>
            o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildServicesWithDb(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration config,
        string dbName)
    {
        var services = new ServiceCollection();
        services.AddSingleton(userManager);
        services.AddSingleton(roleManager);
        services.AddSingleton(config);
        services.AddDbContext<SsoDbContext>(o => o.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    private static SsoDbContext OpenDb(string dbName) =>
        new(new DbContextOptionsBuilder<SsoDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task InitializeAsync_CreatesAdmin_WhenMissing()
    {
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        var services = BuildServices(userManagerMock.Object, MockRoleManager().Object, BuildConfig());

        await SeedData.InitializeAsync(services);

        userManagerMock.Verify(m => m.CreateAsync(
            It.Is<ApplicationUser>(u => u.Email == "admin@example.com" && u.IsActive),
            "AdminPassword123!"), Times.Once);

        userManagerMock.Verify(m => m.AddToRoleAsync(
            It.IsAny<ApplicationUser>(), SeedData.AdminRole), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_Skips_WhenAdminAlreadyExists()
    {
        var userManagerMock = MockUserManager();
        var existingAdmin = new ApplicationUser { Email = "admin@example.com" };
        userManagerMock.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(existingAdmin);
        userManagerMock.Setup(m => m.IsInRoleAsync(existingAdmin, SeedData.AdminRole)).ReturnsAsync(true);

        var services = BuildServices(userManagerMock.Object, MockRoleManager().Object, BuildConfig());

        await SeedData.InitializeAsync(services);

        userManagerMock.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_FirstRun_CreatesMvcClientAppGroupsAndAssignsAdmin()
    {
        var dbName = Guid.NewGuid().ToString();
        var admin = new ApplicationUser { Id = "admin-1", Email = "admin@example.com" };
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(admin);
        userManagerMock.Setup(m => m.IsInRoleAsync(admin, SeedData.AdminRole)).ReturnsAsync(true);

        await SeedData.InitializeAsync(
            BuildServicesWithDb(userManagerMock.Object, MockRoleManager().Object, BuildConfig(), dbName));

        using var db = OpenDb(dbName);
        var app = await db.Tenants.SingleAsync(t => t.Name == "MvcClientApp");
        Assert.True(app.IsActive);

        var groups = await db.Groups.Where(g => g.TenantAppId == app.Id).ToListAsync();
        Assert.Contains(groups, g => g.Name == "MvcClientApp-Admin" && g.Level == 0);
        Assert.Contains(groups, g => g.Name == "MvcClientApp-Users" && g.Level == 1);
        Assert.Equal(2, await db.UserGroups.CountAsync(ug => ug.UserId == "admin-1"));
    }

    [Fact]
    public async Task InitializeAsync_DoesNotReactivateOrOverwrite_ExistingMvcClientApp()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var setup = OpenDb(dbName))
        {
            setup.Tenants.Add(new TenantApp
            {
                Name = "MvcClientApp",
                ReturnUrl = "https://custom.example.com/callback",
                IsActive = false      // an admin disabled it in the UI
            });
            setup.SaveChanges();
        }

        var admin = new ApplicationUser { Id = "admin-1", Email = "admin@example.com" };
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(admin);
        userManagerMock.Setup(m => m.IsInRoleAsync(admin, SeedData.AdminRole)).ReturnsAsync(true);

        await SeedData.InitializeAsync(
            BuildServicesWithDb(userManagerMock.Object, MockRoleManager().Object, BuildConfig(), dbName));

        using var db = OpenDb(dbName);
        var app = await db.Tenants.SingleAsync(t => t.Name == "MvcClientApp");
        Assert.False(app.IsActive);
        Assert.Equal("https://custom.example.com/callback", app.ReturnUrl);
        Assert.Empty(db.Groups);          // deleted/never-created groups are not resurrected
        Assert.Empty(db.UserGroups);
    }
}