namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Represents one source branch candidate used for mergerfs branch planning.
/// </summary>
internal sealed class MergerfsSourceBranchCandidate
{

	/// <summary>
	/// Initializes a new instance of the <see cref="MergerfsSourceBranchCandidate"/> class.
	/// </summary>
	/// <param name="sourceName">Logical source name used for ordering and link naming.</param>
	/// <param name="sourcePath">Fully-qualified absolute source path to include as a mergerfs source branch.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sourceName"/> or <paramref name="sourcePath"/> is null, empty, or invalid.
	/// </exception>
	public MergerfsSourceBranchCandidate(string sourceName, string sourcePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

		string trimmedSourceName = sourceName.Trim();
		if (trimmedSourceName.Length == 0)
		{
			throw new ArgumentException(
				"Source name must not be empty after trimming.",
				nameof(sourceName));
		}

		SourceName = trimmedSourceName;
		SourcePath = PathSafetyPolicy.NormalizeFullyQualifiedPath(sourcePath, nameof(sourcePath));
	}

	/// <summary>
	/// Gets the logical source name.
	/// </summary>
	public string SourceName
	{
		get;
	}

	/// <summary>
	/// Gets the fully-qualified absolute source path.
	/// </summary>
	public string SourcePath
	{
		get;
	}
}
