using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Data;

public class SsoDbContext : IdentityDbContext<IdentityUser>
{
    #region Ctor
    public SsoDbContext(DbContextOptions<SsoDbContext> options) : base(options)
    {
    }

    protected SsoDbContext()
    {
    }

    #endregion 

    #region DbSets

    public DbSet<TenantApp> Tenants { get; set; }

    public DbSet<UserGroup> UserGroups { get; set; }

    #endregion


    #region Overrides
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

     
        builder.Entity<TenantApp>()
            .HasIndex(t => t.Name)
            .IsUnique();

            builder.Entity<Group>()
            .HasIndex(g => new { g.Name, g.TenantAppId }).IsUnique();

            builder.Entity<Group>().HasOne(g => g.TenantApp)
            .WithMany(t => t.Groups)
            .HasForeignKey(g => g.TenantAppId);

      
        builder.Entity<UserGroup>()
            .HasKey(ug => new { ug.UserId, ug.GroupId });

      
        builder.Entity<UserGroup>()
            .HasOne(ug => ug.Group)
            .WithMany(g => g.UserGroups) 
            .HasForeignKey(ug => ug.GroupId);

      
        builder.Entity<UserGroup>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(ug => ug.UserId);


    }

    #endregion
}
