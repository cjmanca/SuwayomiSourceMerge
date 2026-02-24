namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Builds canonical Comick endpoint URIs used by direct and gateway request paths.
/// </summary>
internal static class ComickEndpointUriBuilder
{
	/// <summary>
	/// Relative path for title search requests.
	/// </summary>
	private const string SearchPath = "v1.0/search/";

	/// <summary>
	/// Relative path prefix for comic-detail requests.
	/// </summary>
	private const string ComicPath = "comic/";

	/// <summary>
	/// Builds one search endpoint URI.
	/// </summary>
	/// <param name="baseUri">Comick API base URI.</param>
	/// <param name="query">Search query text.</param>
	/// <returns>Resolved absolute request URI.</returns>
	public static Uri BuildSearchUri(Uri baseUri, string query)
	{
		ArgumentNullException.ThrowIfNull(baseUri);
		ArgumentException.ThrowIfNullOrWhiteSpace(query);

		return new Uri(
			baseUri,
			$"{SearchPath}?q={Uri.EscapeDataString(query.Trim())}");
	}

	/// <summary>
	/// Builds one comic-detail endpoint URI.
	/// </summary>
	/// <param name="baseUri">Comick API base URI.</param>
	/// <param name="slug">Comic slug.</param>
	/// <returns>Resolved absolute request URI.</returns>
	public static Uri BuildComicUri(Uri baseUri, string slug)
	{
		ArgumentNullException.ThrowIfNull(baseUri);
		ArgumentException.ThrowIfNullOrWhiteSpace(slug);

		return new Uri(
			baseUri,
			$"{ComicPath}{Uri.EscapeDataString(slug.Trim())}/");
	}
}
