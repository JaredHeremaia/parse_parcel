using Shipping.Core.Model;

namespace Shipping.Core.Storage;

/// <summary>
/// Persistence port for the package catalogue. Implemented in-memory by default
/// and by Postgres when configured; the domain never knows which is in play.
/// </summary>
public interface IPackageTypeStore
{
    Task<IReadOnlyList<PackageType>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PackageType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive lookup by name, e.g. "small".</summary>
    Task<PackageType?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task AddAsync(PackageType packageType, CancellationToken cancellationToken = default);

    Task UpdateAsync(PackageType packageType, CancellationToken cancellationToken = default);

    /// <returns>True if a package type was removed, false if it did not exist.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
