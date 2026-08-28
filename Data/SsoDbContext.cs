using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

    public DbSet<Models.TenantApp> Tenants { get; set; }

    #endregion


    #region Overrides
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        //Configure unique constraint on TenantApp.Name
        builder.Entity<Models.TenantApp>()
            .HasIndex(t => t.Name)
            .IsUnique();

    }

    #endregion
}
