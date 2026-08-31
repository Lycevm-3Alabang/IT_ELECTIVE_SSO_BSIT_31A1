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

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminCredentials:Email"] = "admin@example.com",
                ["AdminCredentials:Password"] = "AdminPassword123!",
            })
            .Build();

    private static IServiceProvider BuildServices(UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddSingleton(userManager);
        services.AddSingleton(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task InitializeAsync_CreatesAdmin_WhenMissing()
    {
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var services = BuildServices(userManagerMock.Object, BuildConfig());

        await SeedData.InitializeAsync(services);

        userManagerMock.Verify(m => m.CreateAsync(
            It.Is<ApplicationUser>(u => u.IsActive && u.Email == "admin@example.com"),
            "AdminPassword123!"), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_Skips_WhenAdminAlreadyExists()
    {
        var userManagerMock = MockUserManager();
        userManagerMock.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser { Email = "admin@example.com" });

        var services = BuildServices(userManagerMock.Object, BuildConfig());

        await SeedData.InitializeAsync(services);

        userManagerMock.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }
}