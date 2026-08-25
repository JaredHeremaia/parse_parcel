using System.Globalization;

namespace Shipping.Cli;

/// <summary>Raised when the user's arguments do not make sense. Reported as usage help.</summary>
internal sealed class CommandLineException : Exception
{
    public CommandLineException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Small hand-rolled parser: positional words ("packages", "add") followed by
/// "--option value", "--option=value" or bare "--flag". Hand-rolled to keep the
/// CLI dependency-free and the parsing rules unit testable.
/// </summary>
internal sealed class CommandLineArgs
{
    private const string FlagValue = "true";

    private readonly IReadOnlyDictionary<string, string> _options;

    private CommandLineArgs(IReadOnlyList<string> positionals, IReadOnlyDictionary<string, string> options)
    {
        Positionals = positionals;
        _options = options;
    }

    public IReadOnlyList<string> Positionals { get; }

    public IReadOnlyDictionary<string, string> Options => _options;

    public static CommandLineArgs Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var positionals = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(arg);
                continue;
            }

            var name = arg[2..];
            string value;

            var equals = name.IndexOf('=', StringComparison.Ordinal);
            if (equals >= 0)
            {
                value = name[(equals + 1)..];
                name = name[..equals];
            }
            else if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++i];
            }
            else
            {
                value = FlagValue;
            }

            if (name.Length == 0)
            {
                throw new CommandLineException($"'{arg}' is not a valid option.");
            }

            options[name] = value;
        }

        return new CommandLineArgs(positionals, options);
    }

    public string? Positional(int index)
        => index < Positionals.Count ? Positionals[index] : null;

    public bool HasOption(string name) => _options.ContainsKey(name);

    public string? GetString(string name)
        => _options.TryGetValue(name, out var value) ? value : null;

    public string GetRequiredString(string name)
        => GetString(name) ?? throw new CommandLineException($"--{name} is required.");

    public int? GetInt(string name)
    {
        var raw = GetString(name);
        if (raw is null)
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new CommandLineException($"--{name} must be a whole number, but was '{raw}'.");
    }

    public int GetRequiredInt(string name)
        => GetInt(name) ?? throw new CommandLineException($"--{name} is required.");

    public decimal? GetDecimal(string name)
    {
        var raw = GetString(name);
        if (raw is null)
        {
            return null;
        }

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new CommandLineException($"--{name} must be a number, but was '{raw}'.");
    }

    public decimal GetRequiredDecimal(string name)
        => GetDecimal(name) ?? throw new CommandLineException($"--{name} is required.");

    public Guid GetRequiredGuid(string positional, int index)
    {
        var raw = Positional(index)
            ?? throw new CommandLineException($"A package type {positional} is required.");

        return Guid.TryParse(raw, out var id)
            ? id
            : throw new CommandLineException($"'{raw}' is not a valid package type id.");
    }
}
