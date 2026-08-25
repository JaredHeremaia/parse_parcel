using Microsoft.EntityFrameworkCore;
using Shipping.Core.Model;

namespace Shipping.Persistence.Postgres;

public sealed class PackagingDbContext : DbContext
{
    public PackagingDbContext(DbContextOptions<PackagingDbContext> options)
        : base(options)
    {
    }

    public DbSet<PackageType> PackageTypes => Set<PackageType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var packageType = modelBuilder.Entity<PackageType>();

        packageType.ToTable("package_types");
        packageType.HasKey(p => p.Id);

        packageType.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        packageType.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(PackageType.MaxNameLength)
            .IsRequired();

        // Names are the alternate lookup key ("/api/packages/small"), so keep them unique.
        packageType.HasIndex(p => p.Name).IsUnique();

        packageType.Property(p => p.LengthMm).HasColumnName("length_mm");
        packageType.Property(p => p.BreadthMm).HasColumnName("breadth_mm");
        packageType.Property(p => p.HeightMm).HasColumnName("height_mm");

        packageType.Property(p => p.Cost)
            .HasColumnName("cost")
            .HasPrecision(10, 2);

        // Derived from the mapped columns; nothing to store.
        packageType.Ignore(p => p.MaxDimensions);
        packageType.Ignore(p => p.MaxVolumeMm3);
    }
}
