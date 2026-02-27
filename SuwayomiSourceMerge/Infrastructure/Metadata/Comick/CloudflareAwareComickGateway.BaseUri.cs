namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Base-URI normalization helpers for <see cref="CloudflareAwareComickGateway"/>.
/// </summary>
internal sealed partial class CloudflareAwareComickGateway
{
	/// <summary>
	/// Normalizes one Comick API base URI to an absolute http/https URI with exactly one trailing slash.
	/// </summary>
	/// <param name="baseUri">Base URI to normalize.</param>
	/// <returns>Normalized base URI.</returns>
	/// <exception cref="ArgumentException">Thrown when URI is not absolute or does not use http/https.</exception>
	private static Uri NormalizeBaseUri(Uri baseUri)
	{
		ArgumentNullException.ThrowIfNull(baseUri);
		if (!baseUri.IsAbsoluteUri)
		{
			throw new ArgumentException("Comick API base URI must be absolute.", nameof(baseUri));
		}

		if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("Comick API base URI must use http or https.", nameof(baseUri));
		}

		return new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
	}
}
