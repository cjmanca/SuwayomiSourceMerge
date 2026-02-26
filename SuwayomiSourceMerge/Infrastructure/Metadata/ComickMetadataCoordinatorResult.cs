namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Represents one deterministic result from per-title metadata coordination.
/// </summary>
internal sealed class ComickMetadataCoordinatorResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ComickMetadataCoordinatorResult"/> class.
	/// </summary>
	/// <param name="apiCalled">Whether a Comick search API request was attempted.</param>
	/// <param name="hadServiceInterruption">
	/// Whether Comick API interaction indicated a service interruption that should fail the merge pass.
	/// </param>
	/// <param name="coverExists">Whether <c>cover.jpg</c> exists after coordination completes.</param>
	/// <param name="detailsExists">Whether <c>details.json</c> exists after coordination completes.</param>
	public ComickMetadataCoordinatorResult(
		bool apiCalled,
		bool hadServiceInterruption,
		bool coverExists,
		bool detailsExists)
	{
		ApiCalled = apiCalled;
		HadServiceInterruption = hadServiceInterruption;
		CoverExists = coverExists;
		DetailsExists = detailsExists;
	}

	/// <summary>
	/// Gets a value indicating whether a Comick search API request was attempted.
	/// </summary>
	public bool ApiCalled
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether Comick API interaction indicated a service interruption.
	/// </summary>
	public bool HadServiceInterruption
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether <c>cover.jpg</c> exists after coordination completes.
	/// </summary>
	public bool CoverExists
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether <c>details.json</c> exists after coordination completes.
	/// </summary>
	public bool DetailsExists
	{
		get;
	}
}
