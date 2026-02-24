namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Ensures override details.json metadata exists by using non-overwrite checks, source seeding, API-first generation, and ComicInfo fallback behavior.
/// </summary>
internal interface IOverrideDetailsService
{
	/// <summary>
	/// Ensures details.json metadata exists for the requested canonical title.
	/// </summary>
	/// <param name="request">Request containing preferred override path, sources, title, and rendering mode.</param>
	/// <returns>Deterministic outcome details describing whether seeding, generation, or skip logic was applied.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
	OverrideDetailsResult EnsureDetailsJson(OverrideDetailsRequest request);

}
