namespace SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Parses textual logging level values into <see cref="LogLevel"/> values.
/// </summary>
/// <remarks>
/// Validation and runtime logger construction use this class so both paths
/// share the same accepted tokens and normalization behavior.
/// </remarks>
internal static class LogLevelParser
{
	/// <summary>
	/// Canonical token-to-level mapping used for parsing and display.
	/// </summary>
	private static readonly (string Token, LogLevel Level)[] SupportedTokens =
	[
		("trace", LogLevel.Trace),
		("debug", LogLevel.Debug),
		("warning", LogLevel.Warning),
		("error", LogLevel.Error),
		("none", LogLevel.None)
	];

	/// <summary>
	/// Gets a human-readable, comma-delimited list of supported level tokens.
	/// </summary>
	public static string SupportedValuesDisplay
	{
		get
		{
			return string.Join(", ", SupportedTokens.Select(static token => token.Token));
		}
	}

	/// <summary>
	/// Attempts to parse a logging level token.
	/// </summary>
	/// <param name="value">Raw token from configuration or user input.</param>
	/// <param name="level">Parsed level when successful; default value otherwise.</param>
	/// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
	public static bool TryParse(string? value, out LogLevel level)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			level = default;
			return false;
		}

		string normalized = Normalize(value);
		foreach ((string token, LogLevel parsedLevel) in SupportedTokens)
		{
			if (!string.Equals(token, normalized, StringComparison.Ordinal))
			{
				continue;
			}

			level = parsedLevel;
			return true;
		}

		level = default;
		return false;
	}

	/// <summary>
	/// Parses a logging level token or throws a descriptive exception when invalid.
	/// </summary>
	/// <param name="value">Raw token from configuration.</param>
	/// <param name="missingValueMessage">Error message used when <paramref name="value"/> is missing.</param>
	/// <returns>The parsed <see cref="LogLevel"/> value.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="missingValueMessage"/> is empty or whitespace.</exception>
	/// <exception cref="InvalidOperationException">Thrown when <paramref name="value"/> is missing or unsupported.</exception>
	public static LogLevel ParseOrThrow(string? value, string missingValueMessage)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(missingValueMessage);

		if (string.IsNullOrWhiteSpace(value))
		{
			throw new InvalidOperationException(missingValueMessage);
		}

		if (TryParse(value, out LogLevel level))
		{
			return level;
		}

		string normalized = Normalize(value);
		throw new InvalidOperationException(
			$"Unsupported logging level '{value}' (normalized: '{normalized}'). Supported values are: {SupportedValuesDisplay}.");
	}

	/// <summary>
	/// Normalizes a token by trimming whitespace and converting it to lowercase invariant form.
	/// </summary>
	/// <param name="value">Raw token to normalize.</param>
	/// <returns>Normalized token suitable for ordinal comparisons.</returns>
	private static string Normalize(string value)
	{
		return value.Trim().ToLowerInvariant();
	}
}
