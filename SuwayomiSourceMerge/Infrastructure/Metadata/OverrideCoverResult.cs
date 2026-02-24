namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Represents the result of ensuring <c>cover.jpg</c> metadata for one canonical title.
/// </summary>
internal sealed class OverrideCoverResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideCoverResult"/> class.
	/// </summary>
	/// <param name="outcome">Terminal ensure outcome.</param>
	/// <param name="coverJpgPath">
	/// Absolute <c>cover.jpg</c> path associated with the operation outcome.
	/// For <see cref="OverrideCoverOutcome.AlreadyExists"/>, this is the existing on-disk cover path.
	/// For all other outcomes, this is the preferred override target path.
	/// </param>
	/// <param name="coverJpgExists">Whether <c>cover.jpg</c> exists on disk after the ensure operation completes.</param>
	/// <param name="existingCoverPath">Absolute existing override cover path when an existing file short-circuited execution.</param>
	/// <param name="coverUri">Resolved cover URI used for download attempts, when available.</param>
	/// <param name="diagnostic">
	/// Deterministic diagnostic text for failure outcomes, including URI-resolution, download, conversion, and write/setup paths.
	/// </param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="coverJpgPath"/> is null, empty, or whitespace.</exception>
	public OverrideCoverResult(
		OverrideCoverOutcome outcome,
		string coverJpgPath,
		bool coverJpgExists,
		string? existingCoverPath,
		Uri? coverUri,
		string? diagnostic)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(coverJpgPath);

		Outcome = outcome;
		CoverJpgPath = coverJpgPath;
		CoverJpgExists = coverJpgExists;
		ExistingCoverPath = existingCoverPath;
		CoverUri = coverUri;
		Diagnostic = diagnostic;
	}

	/// <summary>
	/// Gets the terminal ensure outcome.
	/// </summary>
	public OverrideCoverOutcome Outcome
	{
		get;
	}

	/// <summary>
	/// Gets the absolute <c>cover.jpg</c> path associated with the operation outcome.
	/// For <see cref="OverrideCoverOutcome.AlreadyExists"/>, this is the existing on-disk cover path.
	/// For all other outcomes, this is the preferred override target path.
	/// </summary>
	public string CoverJpgPath
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether <c>cover.jpg</c> exists on disk after this ensure operation.
	/// </summary>
	public bool CoverJpgExists
	{
		get;
	}

	/// <summary>
	/// Gets the absolute existing override cover path when an existing file short-circuited execution.
	/// </summary>
	public string? ExistingCoverPath
	{
		get;
	}

	/// <summary>
	/// Gets the resolved cover URI used for download attempts, when available.
	/// </summary>
	public Uri? CoverUri
	{
		get;
	}

	/// <summary>
	/// Gets deterministic diagnostic text for failure outcomes (URI resolution, download, conversion, or write/setup).
	/// </summary>
	public string? Diagnostic
	{
		get;
	}
}
