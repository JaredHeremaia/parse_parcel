using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shipping.Core.Model;
using Shipping.Core.Storage;

namespace Shipping.Persistence.Postgres;

public static class PostgresStorageExtensions
{
    public static IServiceCollection AddPostgresPackageStore(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<PackagingDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IPackageTypeStore, PostgresPackageTypeStore>();

        return services;
    }

    /// <summary>
    /// Creates the table if it is missing and seeds the published price list.
    /// EnsureCreated keeps this exercise self-contained; a long-lived service would
    /// use EF migrations instead.
    /// </summary>
    public static async Task InitialisePostgresStorageAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PackagingDbContext>();

        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);

        if (await db.PackageTypes.AnyAsync().ConfigureAwait(false))
        {
            return;
        }

        db.PackageTypes.AddRange(StandardPackageTypes.Create());
        await db.SaveChangesAsync().ConfigureAwait(false);
    }
}
