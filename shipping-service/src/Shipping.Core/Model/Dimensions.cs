namespace Shipping.Core.Model;

/// <summary>
/// Package dimensions in whole millimetres. Always positive: an instance that
/// exists is a valid measurement, so nothing downstream has to re-check it.
/// </summary>
public sealed record Dimensions
{
    /// <summary>Upper bound on a single side (100 m), a sanity guard against nonsense input.</summary>
    public const int MaxSideMm = 100_000;

    public Dimensions(int lengthMm, int breadthMm, int heightMm)
    {
        LengthMm = Require(lengthMm, nameof(lengthMm));
        BreadthMm = Require(breadthMm, nameof(breadthMm));
        HeightMm = Require(heightMm, nameof(heightMm));
    }

    public int LengthMm { get; }

    public int BreadthMm { get; }

    public int HeightMm { get; }

    public long VolumeMm3 => (long)LengthMm * BreadthMm * HeightMm;

    /// <summary>Non-throwing factory for parsing untrusted input (HTTP bodies, CLI arguments).</summary>
    public static bool TryCreate(int lengthMm, int breadthMm, int heightMm, out Dimensions? dimensions)
    {
        if (!IsValidSide(lengthMm) || !IsValidSide(breadthMm) || !IsValidSide(heightMm))
        {
            dimensions = null;
            return false;
        }

        dimensions = new Dimensions(lengthMm, breadthMm, heightMm);
        return true;
    }

    /// <summary>
    /// True when a package of these dimensions fits inside <paramref name="limit"/>.
    /// </summary>
    /// <param name="allowRotation">
    /// When true (the default) the package may be turned to fit, so 300x200x150 is
    /// accepted by a 200x300x150 box. Comparing both sides sorted descending is
    /// sufficient for an axis-aligned fit. Set to false to compare length to length,
    /// breadth to breadth and height to height as given.
    /// </param>
    public bool FitsWithin(Dimensions limit, bool allowRotation = true)
    {
        ArgumentNullException.ThrowIfNull(limit);

        if (!allowRotation)
        {
            return LengthMm <= limit.LengthMm
                && BreadthMm <= limit.BreadthMm
                && HeightMm <= limit.HeightMm;
        }

        var package = SortedDescending();
        var box = limit.SortedDescending();

        return package[0] <= box[0] && package[1] <= box[1] && package[2] <= box[2];
    }

    public override string ToString() => $"{LengthMm}x{BreadthMm}x{HeightMm}mm";

    private int[] SortedDescending()
    {
        var sides = new[] { LengthMm, BreadthMm, HeightMm };
        Array.Sort(sides);
        Array.Reverse(sides);
        return sides;
    }

    private static bool IsValidSide(int value) => value is > 0 and <= MaxSideMm;

    private static int Require(int value, string parameterName)
    {
        if (!IsValidSide(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Dimension must be between 1mm and {MaxSideMm}mm.");
        }

        return value;
    }
}
