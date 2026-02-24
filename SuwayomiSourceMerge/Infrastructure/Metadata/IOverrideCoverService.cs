namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Ensures override <c>cover.jpg</c> metadata exists by downloading and converting Comick cover payloads.
/// </summary>
internal interface IOverrideCoverService
{
	/// <summary>
	/// Ensures <c>cover.jpg</c> exists for the requested canonical title override directories.
	/// </summary>
	/// <param name="request">Request containing override paths and Comick cover-key input.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Deterministic outcome details describing whether the cover already existed, was written, or failed.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
	Task<OverrideCoverResult> EnsureCoverJpgAsync(
		OverrideCoverRequest request,
		CancellationToken cancellationToken = default);
}
