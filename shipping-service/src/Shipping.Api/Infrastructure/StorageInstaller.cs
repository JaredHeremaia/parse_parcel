using Shipping.Core.Storage;
using Shipping.Persistence.Postgres;

namespace Shipping.Api.Infrastructure;

/// <summary>
/// Chooses where package types live. In-memory by default so the API runs with no
/// infrastructure; set "Storage:Provider" to "Postgres" for a durable catalogue.
/// This is the only file in the API that knows Postgres exists.
/// </summary>
internal static class StorageInstaller
{
    private const string InMemory = "InMemory";
    private const string Postgres = "Postgres";

    public static IServiceCollection AddPackageStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Storage:Provider"] ?? InMemory;

        if (provider.Equals(InMemory, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IPackageTypeStore>(_ => new InMemoryPackageTypeStore());
            return services;
        }

        if (provider.Equals(Postgres, StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = configuration.GetConnectionString("Packages")
                ?? throw new InvalidOperationException(
                    "Storage:Provider is 'Postgres' but ConnectionStrings:Packages is not configured.");

            services.AddPostgresPackageStore(connectionString);
            return services;
        }

        throw new InvalidOperationException(
            $"Unknown Storage:Provider '{provider}'. Use '{InMemory}' or '{Postgres}'.");
    }

    /// <summary>Creates the schema and seeds the price list when running on Postgres.</summary>
    public static Task InitialisePackageStorageAsync(this WebApplication app)
    {
        var provider = app.Configuration["Storage:Provider"] ?? InMemory;

        return provider.Equals(Postgres, StringComparison.OrdinalIgnoreCase)
            ? app.Services.InitialisePostgresStorageAsync()
            : Task.CompletedTask;
    }
}
