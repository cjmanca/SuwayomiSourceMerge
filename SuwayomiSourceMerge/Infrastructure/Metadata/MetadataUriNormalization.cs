namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Provides shared URI normalization helpers used by metadata infrastructure options.
/// </summary>
internal static class MetadataUriNormalization
{
	/// <summary>
	/// Ensures one absolute URI value is represented with exactly one trailing slash.
	/// </summary>
	/// <param name="value">Absolute URI value.</param>
	/// <returns>Normalized URI containing exactly one trailing slash.</returns>
	public static Uri EnsureTrailingSlash(Uri value)
	{
		ArgumentNullException.ThrowIfNull(value);

		if (!value.IsAbsoluteUri)
		{
			throw new ArgumentException("URI must be absolute.", nameof(value));
		}

		return new Uri(value.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
	}

	/// <summary>
	/// Normalizes one endpoint path to a non-root relative path with exactly one trailing slash.
	/// </summary>
	/// <param name="value">Endpoint path value.</param>
	/// <param name="paramName">Guard parameter name.</param>
	/// <param name="description">Guard message descriptor.</param>
	/// <returns>Normalized endpoint path.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="value"/> is empty/whitespace, absolute, root-only, or includes query/fragment components.
	/// </exception>
	public static string NormalizeEndpointPath(string value, string paramName, string description)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);

		string trimmed = value.Trim();
		if (HasUriSchemePrefix(trimmed))
		{
			throw new ArgumentException($"{description} must be relative and must not include a URI scheme/host.", paramName);
		}

		string normalized = trimmed.TrimStart('/');
		if (normalized.Length == 0)
		{
			throw new ArgumentException($"{description} must not resolve to root.", paramName);
		}

		if (normalized.IndexOf('?') >= 0 || normalized.IndexOf('#') >= 0)
		{
			throw new ArgumentException($"{description} must not include query or fragment components.", paramName);
		}

		return normalized.TrimEnd('/') + "/";
	}

	/// <summary>
	/// Determines whether one value starts with a URI scheme token.
	/// </summary>
	/// <param name="value">Raw value.</param>
	/// <returns><see langword="true"/> when a URI scheme prefix is present; otherwise <see langword="false"/>.</returns>
	public static bool HasUriSchemePrefix(string value)
	{
		int schemeSeparatorIndex = value.IndexOf(':');
		if (schemeSeparatorIndex <= 0)
		{
			return false;
		}

		ReadOnlySpan<char> schemeToken = value.AsSpan(0, schemeSeparatorIndex);
		if (!char.IsLetter(schemeToken[0]))
		{
			return false;
		}

		for (int index = 1; index < schemeToken.Length; index++)
		{
			char character = schemeToken[index];
			if (!char.IsLetterOrDigit(character) && character != '+' && character != '-' && character != '.')
			{
				return false;
			}
		}

		return true;
	}
}
