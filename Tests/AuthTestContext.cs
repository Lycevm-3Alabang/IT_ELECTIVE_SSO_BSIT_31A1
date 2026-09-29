// Tests/AuthTestContext.cs
using Data;
using Gateway.Controllers;
using Gateway.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Models.Entities;
using Moq;

namespace Data.Tests;

public class AuthTestContext
{
    public const string ReturnUrl = "https://client.example.com/callback";

    public SsoDbContext Db { get; }
    public Mock<UserManager<ApplicationUser>> UserManager { get; }
    public Mock<SignInManager<ApplicationUser>> SignInManager { get; }
    public AuthController Controller { get; }

    public AuthTestContext()
    {
        Db = new SsoDbContext(new DbContextOptionsBuilder<SsoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        UserManager = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object,
            null!, null!, null!, null!, null!, null!, null!, null!);

        SignInManager = new Mock<SignInManager<ApplicationUser>>(
            UserManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Issuer"] = "SSOGateway",
                ["JwtSettings:Audience"] = "SSOClientApps",
                ["JwtSettings:SecretKey"] = "Your_Super_Secret_Key_Here_Make_It_Long",
                ["JwtSettings:ExpiryHours"] = "9"
            })
            .Build();

        var audit = new AuditService(Db);

        Controller = new AuthController(
            SignInManager.Object, UserManager.Object, Db,
            new ReturnUrlValidator(Db, audit), audit, new JwtTokenService(config))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    public TenantApp AddApp(string name = "SalesApp", string returnUrl = ReturnUrl)
    {
        var app = new TenantApp { Name = name, ReturnUrl = returnUrl, IsActive = true };
        Db.Tenants.Add(app);
        Db.SaveChanges();
        return app;
    }

    public ApplicationUser AddUser(bool isActive = true)
    {
        var user = new ApplicationUser
        {
            Id = "u1",
            Email = "user@example.com",
            UserName = "user@example.com",
            IsActive = isActive
        };
        UserManager.Setup(m => m.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        UserManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        return user;
    }

    public Group AddGroupForUser(TenantApp app, ApplicationUser user, string name, int level)
    {
        var group = new Group { Name = name, Level = level, TenantAppId = app.Id };
        Db.Groups.Add(group);
        Db.SaveChanges();
        Db.UserGroups.Add(new UserGroup { UserId = user.Id, GroupId = group.Id });
        Db.SaveChanges();
        return group;
    }
}