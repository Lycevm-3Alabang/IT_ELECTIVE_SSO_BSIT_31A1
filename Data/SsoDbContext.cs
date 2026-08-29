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

    }

    #endregion
}
