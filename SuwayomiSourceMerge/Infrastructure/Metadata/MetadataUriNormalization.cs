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
}
