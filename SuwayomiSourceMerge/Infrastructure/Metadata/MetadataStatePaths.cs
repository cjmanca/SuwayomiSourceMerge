namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Resolves canonical metadata state file paths under a configured state root.
/// </summary>
internal sealed class MetadataStatePaths
{
	/// <summary>
	/// Canonical metadata state file name.
	/// </summary>
	public const string MetadataStateFileName = "metadata_state.json";

	/// <summary>
	/// Canonical backup file name used when a corrupt state file is recovered.
	/// </summary>
	public const string MetadataStateCorruptFileName = "metadata_state.corrupt.json";

	/// <summary>
	/// Canonical backup directory name used when a directory exists at the metadata state file path.
	/// </summary>
	public const string MetadataStateCorruptDirectoryName = "metadata_state.corrupt.dir";

	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataStatePaths"/> class.
	/// </summary>
	/// <param name="stateRootPath">State root directory path.</param>
	public MetadataStatePaths(string stateRootPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stateRootPath);

		StateRootPath = Path.GetFullPath(stateRootPath);
		MetadataStateFilePath = Path.Combine(StateRootPath, MetadataStateFileName);
		MetadataStateCorruptFilePath = Path.Combine(StateRootPath, MetadataStateCorruptFileName);
		MetadataStateCorruptDirectoryPath = Path.Combine(StateRootPath, MetadataStateCorruptDirectoryName);
	}

	/// <summary>
	/// Gets the normalized metadata state root path.
	/// </summary>
	public string StateRootPath
	{
		get;
	}

	/// <summary>
	/// Gets the primary metadata state file path.
	/// </summary>
	public string MetadataStateFilePath
	{
		get;
	}

	/// <summary>
	/// Gets the backup path used for corrupt metadata state recovery.
	/// </summary>
	public string MetadataStateCorruptFilePath
	{
		get;
	}

	/// <summary>
	/// Gets the backup path used when a directory corrupts the metadata state file location.
	/// </summary>
	public string MetadataStateCorruptDirectoryPath
	{
		get;
	}
}
