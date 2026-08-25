using Shipping.Core.Model;
using Shipping.Core.Storage;

namespace Shipping.Core;

/// <summary>
/// Application service for managing the package catalogue. Owns the rules the
/// store should not care about (names are unique, a package type must exist
/// before it can be changed) so the API and CLI stay thin.
/// </summary>
public sealed class PackageCatalog
{
    private readonly IPackageTypeStore _store;

    public PackageCatalog(IPackageTypeStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<PackageType>> ListAsync(CancellationToken cancellationToken = default)
        => _store.GetAllAsync(cancellationToken);

    /// <summary>Looks up a package type by id or by name, e.g. "small".</summary>
    public async Task<Result<PackageType>> GetAsync(
        string idOrName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            return Result<PackageType>.Invalid("Provide a package type id or name.");
        }

        var key = idOrName.Trim();

        var match = Guid.TryParse(key, out var id)
            ? await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
            : await _store.GetByNameAsync(key, cancellationToken).ConfigureAwait(false);

        return match is null
            ? Result<PackageType>.NotFound($"No package type found for '{key}'.")
            : Result<PackageType>.Success(match);
    }

    public async Task<Result<PackageType>> CreateAsync(
        string name,
        Dimensions dimensions,
        decimal cost,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dimensions);

        var validation = ValidateName(name);
        if (validation is not null)
        {
            return Result<PackageType>.Invalid(validation);
        }

        var existing = await _store.GetByNameAsync(name, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result<PackageType>.Conflict($"A package type named '{existing.Name}' already exists.");
        }

        PackageType packageType;
        try
        {
            packageType = new PackageType(Guid.NewGuid(), name, dimensions, cost);
        }
        catch (ArgumentException ex)
        {
            return Result<PackageType>.Invalid(ex.Message);
        }

        await _store.AddAsync(packageType, cancellationToken).ConfigureAwait(false);
        return Result<PackageType>.Success(packageType);
    }

    public async Task<Result<PackageType>> UpdateAsync(
        Guid id,
        string name,
        Dimensions dimensions,
        decimal cost,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dimensions);

        var validation = ValidateName(name);
        if (validation is not null)
        {
            return Result<PackageType>.Invalid(validation);
        }

        var packageType = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (packageType is null)
        {
            return Result<PackageType>.NotFound($"No package type found with id '{id}'.");
        }

        var clash = await _store.GetByNameAsync(name, cancellationToken).ConfigureAwait(false);
        if (clash is not null && clash.Id != id)
        {
            return Result<PackageType>.Conflict($"A package type named '{clash.Name}' already exists.");
        }

        try
        {
            packageType.Update(name, dimensions, cost);
        }
        catch (ArgumentException ex)
        {
            return Result<PackageType>.Invalid(ex.Message);
        }

        await _store.UpdateAsync(packageType, cancellationToken).ConfigureAwait(false);
        return Result<PackageType>.Success(packageType);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _store.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

        return deleted
            ? Result.Success()
            : Result.NotFound($"No package type found with id '{id}'.");
    }

    private static string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name is required.";
        }

        return name.Trim().Length > PackageType.MaxNameLength
            ? $"Name must be {PackageType.MaxNameLength} characters or fewer."
            : null;
    }
}
