namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Defines direct Comick API access behavior for search and comic-detail endpoints.
/// </summary>
internal interface IComickDirectApiClient
{
	/// <summary>
	/// Queries the Comick search endpoint for one title query.
	/// </summary>
	/// <param name="query">Search query text.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Typed search response result.</returns>
	Task<ComickDirectApiResult<ComickSearchResponse>> SearchAsync(
		string query,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Queries the Comick comic endpoint for one slug.
	/// </summary>
	/// <param name="slug">Comick comic slug.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Typed comic-detail response result.</returns>
	Task<ComickDirectApiResult<ComickComicResponse>> GetComicAsync(
		string slug,
		CancellationToken cancellationToken = default);
}
