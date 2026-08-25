using System.Collections.Concurrent;
using Shipping.Core.Model;

namespace Shipping.Core.Storage;

/// <summary>
/// Default store so the API and tests run with no infrastructure. Data lives for
/// the lifetime of the process; point the API at Postgres for durability.
/// </summary>
public sealed class InMemoryPackageTypeStore : IPackageTypeStore
{
    private readonly ConcurrentDictionary<Guid, PackageType> _packageTypes = new();

    /// <param name="seed">Package types to start with; defaults to the published price list.</param>
    public InMemoryPackageTypeStore(IEnumerable<PackageType>? seed = null)
    {
        foreach (var packageType in seed ?? StandardPackageTypes.Create())
        {
            _packageTypes[packageType.Id] = packageType;
        }
    }

    public Task<IReadOnlyList<PackageType>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PackageType> all = _packageTypes.Values
            .OrderBy(p => p.Cost)
            .ThenBy(p => p.MaxVolumeMm3)
            .ToList();

        return Task.FromResult(all);
    }

    public Task<PackageType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_packageTypes.TryGetValue(id, out var packageType) ? packageType : null);

    public Task<PackageType?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var match = _packageTypes.Values
            .FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(match);
    }

    public Task AddAsync(PackageType packageType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageType);

        _packageTypes[packageType.Id] = packageType;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(PackageType packageType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageType);

        _packageTypes[packageType.Id] = packageType;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_packageTypes.TryRemove(id, out _));
}
