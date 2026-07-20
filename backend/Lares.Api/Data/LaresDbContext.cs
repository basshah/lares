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
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<DeviceLabel> DeviceLabels => Set<DeviceLabel>();

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

        builder.Entity<Area>(area =>
        {
            area.Property(a => a.Name).HasMaxLength(100);
            area.HasOne(a => a.Home)
                .WithMany()
                .HasForeignKey(a => a.HomeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Label>(label =>
        {
            label.Property(l => l.Name).HasMaxLength(50);
            label.HasOne(l => l.Home)
                .WithMany()
                .HasForeignKey(l => l.HomeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Device>(device =>
        {
            device.Property(d => d.Name).HasMaxLength(100);
            device.Property(d => d.Type).HasConversion<string>().HasMaxLength(20);
            device.Property(d => d.State).HasMaxLength(50);

            device.HasOne(d => d.Home)
                .WithMany()
                .HasForeignKey(d => d.HomeId)
                .OnDelete(DeleteBehavior.Cascade);

            device.HasOne(d => d.Area)
                .WithMany()
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.SetNull);

            device.ComplexProperty(d => d.Attributes, attrs => attrs.ToJson());
        });

        builder.Entity<DeviceLabel>(deviceLabel =>
        {
            deviceLabel.HasIndex(dl => new { dl.DeviceId, dl.LabelId }).IsUnique();
            deviceLabel.HasOne(dl => dl.Device)
                .WithMany()
                .HasForeignKey(dl => dl.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
            deviceLabel.HasOne(dl => dl.Label)
                .WithMany()
                .HasForeignKey(dl => dl.LabelId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
