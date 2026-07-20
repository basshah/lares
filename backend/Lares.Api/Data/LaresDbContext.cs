using Lares.Api.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Lares.Api.Data;

public class LaresDbContext(DbContextOptions<LaresDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Home> Homes => Set<Home>();
    public DbSet<Membership> Memberships => Set<Membership>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(token =>
        {
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.Property(t => t.TokenHash).HasMaxLength(64);
            token.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Home>(home =>
        {
            home.HasIndex(h => h.InviteCode).IsUnique();
            home.Property(h => h.InviteCode).HasMaxLength(16);
            home.Property(h => h.Name).HasMaxLength(100);
        });

        builder.Entity<Membership>(membership =>
        {
            membership.HasIndex(m => m.UserId).IsUnique();
            membership.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
            membership.HasOne(m => m.Home)
                .WithMany()
                .HasForeignKey(m => m.HomeId)
                .OnDelete(DeleteBehavior.Cascade);
            membership.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
