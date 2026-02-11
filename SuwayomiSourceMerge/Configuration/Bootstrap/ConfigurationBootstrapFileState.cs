namespace SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Describes how an individual configuration file was resolved during bootstrap.
/// </summary>
public sealed record ConfigurationBootstrapFileState
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConfigurationBootstrapFileState"/> class.
	/// </summary>
	/// <param name="fileName">Canonical configuration file name.</param>
	/// <param name="filePath">Absolute path to the file.</param>
	/// <param name="wasCreated">Indicates whether bootstrap created the file.</param>
	/// <param name="wasMigrated">Indicates whether bootstrap created the file from legacy input.</param>
	/// <param name="usedDefaults">Indicates whether bootstrap wrote default values.</param>
	/// <param name="wasSelfHealed">Indicates whether bootstrap patched missing settings values in an existing file.</param>
	public ConfigurationBootstrapFileState(
		string fileName,
		string filePath,
		bool wasCreated,
		bool wasMigrated,
		bool usedDefaults,
		bool wasSelfHealed)
	{
		FileName = string.IsNullOrWhiteSpace(fileName)
			? throw new ArgumentException("File name must not be null or whitespace.", nameof(fileName))
			: fileName;
		FilePath = string.IsNullOrWhiteSpace(filePath)
			? throw new ArgumentException("File path must not be null or whitespace.", nameof(filePath))
			: filePath;
		WasCreated = wasCreated;
		WasMigrated = wasMigrated;
		UsedDefaults = usedDefaults;
		WasSelfHealed = wasSelfHealed;
	}

	/// <summary>
	/// Gets the canonical configuration file name.
	/// </summary>
	public string FileName
	{
		get;
	}

	/// <summary>
	/// Gets the absolute path to the configuration file.
	/// </summary>
	public string FilePath
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether bootstrap created the file.
	/// </summary>
	public bool WasCreated
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether bootstrap created the file from legacy input.
	/// </summary>
	public bool WasMigrated
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether bootstrap wrote default values.
	/// </summary>
	public bool UsedDefaults
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether bootstrap patched missing settings values in an existing file.
	/// </summary>
	public bool WasSelfHealed
	{
		get;
	}
}
