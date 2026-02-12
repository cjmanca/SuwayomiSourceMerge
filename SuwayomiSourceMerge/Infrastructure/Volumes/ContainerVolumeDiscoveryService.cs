namespace SuwayomiSourceMerge.Infrastructure.Volumes;

/// <summary>
/// Discovers mapped source and override volumes from container root directories.
/// </summary>
/// <remarks>
/// Discovery is limited to direct-child directories, which matches the configured container layout contract.
/// Missing roots are reported as warnings rather than treated as fatal errors.
/// </remarks>
internal sealed class ContainerVolumeDiscoveryService : IContainerVolumeDiscoveryService
{
	private const string MISSING_ROOT_WARNING_CODE = "VOL-DISC-001";
	private readonly IContainerVolumeFileSystem _fileSystem;

	/// <summary>
	/// Initializes a new instance of the <see cref="ContainerVolumeDiscoveryService"/> class
	/// that uses the real host filesystem.
	/// </summary>
	public ContainerVolumeDiscoveryService()
		: this(new ContainerVolumeFileSystem())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ContainerVolumeDiscoveryService"/> class
	/// with an explicit filesystem dependency.
	/// </summary>
	/// <param name="fileSystem">Filesystem adapter used to normalize paths and enumerate directories.</param>
	internal ContainerVolumeDiscoveryService(IContainerVolumeFileSystem fileSystem)
	{
		_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
	}

	/// <inheritdoc />
	public ContainerVolumeDiscoveryResult Discover(string sourcesRootPath, string overrideRootPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcesRootPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(overrideRootPath);

		List<ContainerVolumeDiscoveryWarning> warnings = [];
		IReadOnlyList<string> sourceVolumePaths = DiscoverVolumesUnderRoot(sourcesRootPath, warnings);
		IReadOnlyList<string> overrideVolumePaths = DiscoverVolumesUnderRoot(overrideRootPath, warnings);

		return new ContainerVolumeDiscoveryResult(sourceVolumePaths, overrideVolumePaths, warnings);
	}

	private IReadOnlyList<string> DiscoverVolumesUnderRoot(
		string rootPath,
		ICollection<ContainerVolumeDiscoveryWarning> warnings)
	{
		string normalizedRootPath = _fileSystem.GetFullPath(rootPath);
		if (!_fileSystem.DirectoryExists(normalizedRootPath))
		{
			warnings.Add(
				new ContainerVolumeDiscoveryWarning(
					MISSING_ROOT_WARNING_CODE,
					normalizedRootPath,
					$"Container volume root path does not exist: {normalizedRootPath}"));
			return [];
		}

		return _fileSystem
			.EnumerateDirectories(normalizedRootPath)
			.Select(_fileSystem.GetFullPath)
			.OrderBy(path => path, StringComparer.Ordinal)
			.ToArray();
	}
}
