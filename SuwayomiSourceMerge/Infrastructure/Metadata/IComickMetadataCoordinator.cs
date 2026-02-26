namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Coordinates per-title Comick metadata orchestration during merge passes.
/// </summary>
internal interface IComickMetadataCoordinator
{
	/// <summary>
	/// Ensures metadata artifacts for one title using independent artifact gates and Comick API orchestration.
	/// </summary>
	/// <param name="request">Coordinator request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Deterministic coordinator result.</returns>
	ComickMetadataCoordinatorResult EnsureMetadata(
		ComickMetadataCoordinatorRequest request,
		CancellationToken cancellationToken = default);
}
