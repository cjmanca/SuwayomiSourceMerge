namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Builds canonical Comick endpoint URIs used by direct and gateway request paths.
/// </summary>
internal static class ComickEndpointUriBuilder
{
	/// <summary>
	/// Builds one search endpoint URI.
	/// </summary>
	/// <param name="baseUri">Comick API base URI.</param>
	/// <param name="searchPath">Relative search endpoint path appended under <paramref name="baseUri"/>.</param>
	/// <param name="query">Search query text.</param>
	/// <returns>Resolved absolute request URI.</returns>
	public static Uri BuildSearchUri(Uri baseUri, string searchPath, string query)
	{
		ArgumentNullException.ThrowIfNull(baseUri);
		ArgumentException.ThrowIfNullOrWhiteSpace(searchPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(query);

		return new Uri(
			baseUri,
			$"{searchPath.Trim()}?q={Uri.EscapeDataString(query.Trim())}");
	}

	/// <summary>
	/// Builds one comic-detail endpoint URI.
	/// </summary>
	/// <param name="baseUri">Comick API base URI.</param>
	/// <param name="comicPath">Relative comic endpoint path prefix appended under <paramref name="baseUri"/>.</param>
	/// <param name="slug">Comic slug.</param>
	/// <returns>Resolved absolute request URI.</returns>
	public static Uri BuildComicUri(Uri baseUri, string comicPath, string slug)
	{
		ArgumentNullException.ThrowIfNull(baseUri);
		ArgumentException.ThrowIfNullOrWhiteSpace(comicPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(slug);

		return new Uri(
			baseUri,
			$"{comicPath.Trim()}{Uri.EscapeDataString(slug.Trim())}/");
	}
}
