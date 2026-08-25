using Microsoft.EntityFrameworkCore;
using Shipping.Core.Model;
using Shipping.Core.Storage;

namespace Shipping.Persistence.Postgres;

internal sealed class PostgresPackageTypeStore : IPackageTypeStore
{
    private readonly PackagingDbContext _db;

    public PostgresPackageTypeStore(PackagingDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<PackageType>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var packageTypes = await _db.PackageTypes
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // The catalogue is a handful of rows, so the cheapest-first ordering is
        // applied here rather than pushing a volume expression into SQL.
        return packageTypes
            .OrderBy(p => p.Cost)
            .ThenBy(p => p.MaxVolumeMm3)
            .ToList();
    }

    public async Task<PackageType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.PackageTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<PackageType?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var normalised = name.Trim().ToLowerInvariant();

        return await _db.PackageTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name.ToLower() == normalised, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(PackageType packageType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageType);

        _db.PackageTypes.Add(packageType);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(PackageType packageType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageType);

        _db.PackageTypes.Update(packageType);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var packageType = await _db.PackageTypes
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (packageType is null)
        {
            return false;
        }

        _db.PackageTypes.Remove(packageType);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
