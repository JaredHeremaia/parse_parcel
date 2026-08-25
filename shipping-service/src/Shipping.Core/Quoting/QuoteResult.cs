using Shipping.Core.Model;

namespace Shipping.Core.Quoting;

/// <summary>Why we cannot offer a packaging solution.</summary>
public enum QuoteRejectionReason
{
    /// <summary>Weight or dimensions were missing, zero or negative.</summary>
    InvalidInput,

    /// <summary>Over the weight limit we can currently move.</summary>
    Overweight,

    /// <summary>Too big for every package type we offer.</summary>
    Oversized,
}

/// <summary>The packaging solution for a package: which box, and what it costs.</summary>
public sealed record PackagingQuote(
    Guid PackageTypeId,
    string PackageTypeName,
    decimal Cost,
    Dimensions Dimensions,
    decimal WeightKg);

/// <summary>
/// Either a quote or an explained rejection. Being unable to package something is
/// a normal outcome of a valid request, not an error, so it is modelled as a value.
/// </summary>
public sealed class QuoteResult
{
    private readonly PackagingQuote? _quote;

    private QuoteResult(PackagingQuote? quote, QuoteRejectionReason? reason, string message)
    {
        _quote = quote;
        RejectionReason = reason;
        Message = message;
    }

    public bool IsQuoted => _quote is not null;

    public QuoteRejectionReason? RejectionReason { get; }

    public string Message { get; }

    public PackagingQuote Quote => _quote
        ?? throw new InvalidOperationException($"No packaging solution: {RejectionReason}.");

    public static QuoteResult Quoted(PackagingQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);

        return new QuoteResult(
            quote,
            null,
            $"{quote.PackageTypeName} package, {quote.Cost:0.00}.");
    }

    public static QuoteResult Rejected(QuoteRejectionReason reason, string message)
        => new(null, reason, message);
}
