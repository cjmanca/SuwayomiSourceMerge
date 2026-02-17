namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Stages and cleans branch-link directories and symbolic links for mergerfs planning.
/// </summary>
internal sealed class BranchLinkStagingService : IBranchLinkStagingService
{
	/// <summary>
	/// File-system adapter dependency.
	/// </summary>
	private readonly IBranchLinkStagingFileSystem _fileSystem;

	/// <summary>
	/// Initializes a new instance of the <see cref="BranchLinkStagingService"/> class.
	/// </summary>
	public BranchLinkStagingService()
		: this(new BranchLinkStagingFileSystem())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BranchLinkStagingService"/> class.
	/// </summary>
	/// <param name="fileSystem">File-system adapter dependency.</param>
	internal BranchLinkStagingService(IBranchLinkStagingFileSystem fileSystem)
	{
		_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
	}

	/// <inheritdoc />
	public void StageBranchLinks(MergerfsBranchPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan);

		string branchDirectoryPath = PathSafetyPolicy.NormalizeFullyQualifiedPath(plan.BranchDirectoryPath, nameof(plan));
		_fileSystem.CreateDirectory(branchDirectoryPath);

		HashSet<string> desiredLinkPaths = new(PathSafetyPolicy.GetPathComparer());
		for (int index = 0; index < plan.BranchLinks.Count; index++)
		{
			MergerfsBranchLinkDefinition branchLink = plan.BranchLinks[index];
			string safeLinkPath = PathSafetyPolicy.EnsureStrictChildPath(
				branchDirectoryPath,
				branchLink.LinkPath,
				nameof(plan));
			desiredLinkPaths.Add(safeLinkPath);

			if (TryValidateExistingLinkTarget(safeLinkPath, branchLink.TargetPath))
			{
				continue;
			}

			DeleteExistingPathIfPresent(safeLinkPath);
			_fileSystem.CreateDirectorySymbolicLink(safeLinkPath, branchLink.TargetPath);
		}

		string[] existingEntries = _fileSystem.EnumerateFileSystemEntries(branchDirectoryPath).ToArray();
		for (int index = 0; index < existingEntries.Length; index++)
		{
			string entryPath = PathSafetyPolicy.EnsureStrictChildPath(
				branchDirectoryPath,
				existingEntries[index],
				nameof(plan));
			if (desiredLinkPaths.Contains(entryPath))
			{
				continue;
			}

			DeleteExistingPathIfPresent(entryPath);
		}
	}

	/// <inheritdoc />
	public void CleanupStaleBranchDirectories(string branchLinksRootPath, IReadOnlySet<string> activeBranchDirectoryPaths)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(branchLinksRootPath);
		ArgumentNullException.ThrowIfNull(activeBranchDirectoryPaths);

		string normalizedRootPath = PathSafetyPolicy.NormalizeFullyQualifiedPath(branchLinksRootPath, nameof(branchLinksRootPath));
		if (!_fileSystem.DirectoryExists(normalizedRootPath))
		{
			return;
		}

		HashSet<string> activeDirectorySet = new(PathSafetyPolicy.GetPathComparer());
		foreach (string activePath in activeBranchDirectoryPaths)
		{
			string normalizedActivePath = PathSafetyPolicy.EnsureStrictChildPath(
				normalizedRootPath,
				activePath,
				nameof(activeBranchDirectoryPaths));
			activeDirectorySet.Add(normalizedActivePath);
		}

		string[] existingDirectories = _fileSystem.EnumerateDirectories(normalizedRootPath).ToArray();
		for (int index = 0; index < existingDirectories.Length; index++)
		{
			string directoryPath = PathSafetyPolicy.EnsureStrictChildPath(
				normalizedRootPath,
				existingDirectories[index],
				nameof(branchLinksRootPath));
			if (activeDirectorySet.Contains(directoryPath))
			{
				continue;
			}

			_fileSystem.DeleteDirectory(directoryPath, recursive: true);
		}
	}

	/// <summary>
	/// Returns whether an existing symbolic-link target matches the desired target.
	/// </summary>
	/// <param name="linkPath">Symbolic-link path.</param>
	/// <param name="desiredTargetPath">Desired target path.</param>
	/// <returns><see langword="true"/> when existing target matches desired target.</returns>
	private bool TryValidateExistingLinkTarget(string linkPath, string desiredTargetPath)
	{
		string? existingTarget = _fileSystem.TryGetDirectoryLinkTarget(linkPath);
		if (string.IsNullOrWhiteSpace(existingTarget))
		{
			return false;
		}

		return PathSafetyPolicy.ArePathsEqual(existingTarget, desiredTargetPath);
	}

	/// <summary>
	/// Deletes an existing file-system entry when present.
	/// </summary>
	/// <param name="path">Entry path.</param>
	private void DeleteExistingPathIfPresent(string path)
	{
		if (_fileSystem.DirectoryExists(path))
		{
			_fileSystem.DeleteDirectory(path, recursive: true);
			return;
		}

		_fileSystem.DeleteFile(path);
	}
}

/// <summary>
/// File-system abstraction for branch-link staging behavior.
/// </summary>
internal interface IBranchLinkStagingFileSystem
{
	/// <summary>
	/// Determines whether a directory exists.
	/// </summary>
	/// <param name="path">Directory path.</param>
	/// <returns><see langword="true"/> when directory exists.</returns>
	bool DirectoryExists(string path);

	/// <summary>
	/// Determines whether a file exists.
	/// </summary>
	/// <param name="path">File path.</param>
	/// <returns><see langword="true"/> when file exists.</returns>
	bool FileExists(string path);

	/// <summary>
	/// Determines whether any file-system entry exists at the given path.
	/// </summary>
	/// <param name="path">Path to evaluate.</param>
	/// <returns><see langword="true"/> when entry exists.</returns>
	bool PathExists(string path);

	/// <summary>
	/// Creates a directory and any missing parents.
	/// </summary>
	/// <param name="path">Directory path.</param>
	void CreateDirectory(string path);

	/// <summary>
	/// Enumerates top-level directories under one root.
	/// </summary>
	/// <param name="path">Root directory.</param>
	/// <returns>Directory paths.</returns>
	IEnumerable<string> EnumerateDirectories(string path);

	/// <summary>
	/// Enumerates top-level file-system entries under one root.
	/// </summary>
	/// <param name="path">Root directory.</param>
	/// <returns>Entry paths.</returns>
	IEnumerable<string> EnumerateFileSystemEntries(string path);

	/// <summary>
	/// Deletes a directory.
	/// </summary>
	/// <param name="path">Directory path.</param>
	/// <param name="recursive">Whether recursive deletion is enabled.</param>
	void DeleteDirectory(string path, bool recursive);

	/// <summary>
	/// Deletes a file.
	/// </summary>
	/// <param name="path">File path.</param>
	void DeleteFile(string path);

	/// <summary>
	/// Creates a directory symbolic link.
	/// </summary>
	/// <param name="linkPath">Link path.</param>
	/// <param name="targetPath">Target path.</param>
	void CreateDirectorySymbolicLink(string linkPath, string targetPath);

	/// <summary>
	/// Returns the resolved link-target path when the path is a directory symbolic link.
	/// </summary>
	/// <param name="linkPath">Link path.</param>
	/// <returns>Resolved absolute target path when available; otherwise <see langword="null"/>.</returns>
	string? TryGetDirectoryLinkTarget(string linkPath);
}

/// <summary>
/// Real file-system implementation for branch-link staging.
/// </summary>
internal sealed class BranchLinkStagingFileSystem : IBranchLinkStagingFileSystem
{
	/// <inheritdoc />
	public bool DirectoryExists(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return Directory.Exists(path);
	}

	/// <inheritdoc />
	public bool FileExists(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return File.Exists(path);
	}

	/// <inheritdoc />
	public bool PathExists(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return File.Exists(path) || Directory.Exists(path);
	}

	/// <inheritdoc />
	public void CreateDirectory(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		Directory.CreateDirectory(path);
	}

	/// <inheritdoc />
	public IEnumerable<string> EnumerateDirectories(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		if (!Directory.Exists(path))
		{
			return [];
		}

		return Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly);
	}

	/// <inheritdoc />
	public IEnumerable<string> EnumerateFileSystemEntries(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		if (!Directory.Exists(path))
		{
			return [];
		}

		return Directory.EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly);
	}

	/// <inheritdoc />
	public void DeleteDirectory(string path, bool recursive)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		if (Directory.Exists(path))
		{
			Directory.Delete(path, recursive);
		}
	}

	/// <inheritdoc />
	public void DeleteFile(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		try
		{
			File.Delete(path);
		}
		catch (FileNotFoundException)
		{
		}
		catch (DirectoryNotFoundException)
		{
		}
	}

	/// <inheritdoc />
	public void CreateDirectorySymbolicLink(string linkPath, string targetPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(linkPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
		Directory.CreateSymbolicLink(linkPath, targetPath);
	}

	/// <inheritdoc />
	public string? TryGetDirectoryLinkTarget(string linkPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(linkPath);

		try
		{
			DirectoryInfo directoryInfo = new(linkPath);
			if (string.IsNullOrWhiteSpace(directoryInfo.LinkTarget))
			{
				return null;
			}

			string linkTarget = directoryInfo.LinkTarget;
			if (Path.IsPathFullyQualified(linkTarget))
			{
				return Path.GetFullPath(linkTarget);
			}

			string? parentDirectoryPath = Path.GetDirectoryName(linkPath);
			if (string.IsNullOrWhiteSpace(parentDirectoryPath))
			{
				return null;
			}

			return Path.GetFullPath(Path.Combine(parentDirectoryPath, linkTarget));
		}
		catch
		{
			return null;
		}
	}
}
