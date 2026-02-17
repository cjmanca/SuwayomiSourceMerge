namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="BranchLinkStagingService"/>.
/// </summary>
public sealed class BranchLinkStagingServiceTests
{
	/// <summary>
	/// Verifies branch staging creates branch directory and desired symbolic links.
	/// </summary>
	[Fact]
	public void StageBranchLinks_Expected_ShouldCreateBranchDirectoryAndLinks()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchRootPath = Path.Combine(temporaryDirectory.Path, "branches");
		string branchDirectoryPath = Path.Combine(branchRootPath, "group-a");
		string sourcePath = Path.Combine(temporaryDirectory.Path, "source");
		string overridePath = Path.Combine(temporaryDirectory.Path, "override");
		RecordingBranchLinkStagingFileSystem fileSystem = new();
		BranchLinkStagingService service = new(fileSystem);
		MergerfsBranchPlan plan = CreatePlan(
			branchDirectoryPath,
			[
				new MergerfsBranchLinkDefinition("00_override", Path.Combine(branchDirectoryPath, "00_override"), overridePath, MergerfsBranchAccessMode.ReadWrite),
				new MergerfsBranchLinkDefinition("10_source", Path.Combine(branchDirectoryPath, "10_source"), sourcePath, MergerfsBranchAccessMode.ReadOnly)
			]);

		service.StageBranchLinks(plan);

		Assert.Contains(branchDirectoryPath, fileSystem.CreatedDirectories);
		Assert.Equal(2, fileSystem.CreatedLinks.Count);
		Assert.Equal(overridePath, fileSystem.CreatedLinks[Path.Combine(branchDirectoryPath, "00_override")]);
		Assert.Equal(sourcePath, fileSystem.CreatedLinks[Path.Combine(branchDirectoryPath, "10_source")]);
	}

	/// <summary>
	/// Verifies staging updates existing mismatched symbolic-link targets.
	/// </summary>
	[Fact]
	public void StageBranchLinks_Edge_ShouldReplaceExistingMismatchedLink()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchDirectoryPath = Path.Combine(temporaryDirectory.Path, "branches", "group-a");
		string linkPath = Path.Combine(branchDirectoryPath, "10_source");
		string oldTarget = Path.Combine(temporaryDirectory.Path, "old");
		string newTarget = Path.Combine(temporaryDirectory.Path, "new");
		RecordingBranchLinkStagingFileSystem fileSystem = new();
		fileSystem.SetExistingDirectory(branchDirectoryPath);
		fileSystem.SetExistingLink(linkPath, oldTarget);
		BranchLinkStagingService service = new(fileSystem);
		MergerfsBranchPlan plan = CreatePlan(
			branchDirectoryPath,
			[
				new MergerfsBranchLinkDefinition("10_source", linkPath, newTarget, MergerfsBranchAccessMode.ReadOnly)
			]);

		service.StageBranchLinks(plan);

		Assert.Contains(linkPath, fileSystem.DeletedDirectories);
		Assert.Equal(newTarget, fileSystem.CreatedLinks[linkPath]);
	}

	/// <summary>
	/// Verifies staging replaces dangling symbolic links that point to mismatched targets.
	/// </summary>
	[Fact]
	public void StageBranchLinks_Edge_ShouldReplaceExistingDanglingMismatchedLink()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchDirectoryPath = Path.Combine(temporaryDirectory.Path, "branches", "group-a");
		string linkPath = Path.Combine(branchDirectoryPath, "10_source");
		string oldTarget = Path.Combine(temporaryDirectory.Path, "missing");
		string newTarget = Path.Combine(temporaryDirectory.Path, "source");
		RecordingBranchLinkStagingFileSystem fileSystem = new();
		fileSystem.SetExistingDirectory(branchDirectoryPath);
		fileSystem.SetExistingDanglingLink(linkPath, oldTarget);
		BranchLinkStagingService service = new(fileSystem);
		MergerfsBranchPlan plan = CreatePlan(
			branchDirectoryPath,
			[
				new MergerfsBranchLinkDefinition("10_source", linkPath, newTarget, MergerfsBranchAccessMode.ReadOnly)
			]);

		service.StageBranchLinks(plan);

		Assert.Contains(linkPath, fileSystem.DeletedFiles);
		Assert.Equal(newTarget, fileSystem.CreatedLinks[linkPath]);
	}

	/// <summary>
	/// Verifies staging removes stale dangling symbolic-link entries not present in desired branch links.
	/// </summary>
	[Fact]
	public void StageBranchLinks_Edge_ShouldDeleteStaleDanglingEntries_WhenNotDesired()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchDirectoryPath = Path.Combine(temporaryDirectory.Path, "branches", "group-a");
		string stalePath = Path.Combine(branchDirectoryPath, "stale_link");
		string desiredPath = Path.Combine(branchDirectoryPath, "10_source");
		RecordingBranchLinkStagingFileSystem fileSystem = new();
		fileSystem.SetExistingDirectory(branchDirectoryPath);
		fileSystem.SetExistingDanglingLink(stalePath, Path.Combine(temporaryDirectory.Path, "missing"));
		BranchLinkStagingService service = new(fileSystem);
		MergerfsBranchPlan plan = CreatePlan(
			branchDirectoryPath,
			[
				new MergerfsBranchLinkDefinition("10_source", desiredPath, Path.Combine(temporaryDirectory.Path, "source"), MergerfsBranchAccessMode.ReadOnly)
			]);

		service.StageBranchLinks(plan);

		Assert.Contains(stalePath, fileSystem.DeletedFiles);
		Assert.Null(fileSystem.TryGetDirectoryLinkTarget(stalePath));
	}

	/// <summary>
	/// Verifies invalid link paths outside branch directory are rejected.
	/// </summary>
	[Fact]
	public void StageBranchLinks_Failure_ShouldThrow_WhenLinkPathIsOutsideBranchDirectory()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchDirectoryPath = Path.Combine(temporaryDirectory.Path, "branches", "group-a");
		string invalidLinkPath = Path.Combine(temporaryDirectory.Path, "other", "outside");
		BranchLinkStagingService service = new(new RecordingBranchLinkStagingFileSystem());
		MergerfsBranchPlan plan = CreatePlan(
			branchDirectoryPath,
			[
				new MergerfsBranchLinkDefinition("10_source", invalidLinkPath, Path.Combine(temporaryDirectory.Path, "target"), MergerfsBranchAccessMode.ReadOnly)
			]);

		Assert.Throws<ArgumentException>(() => service.StageBranchLinks(plan));
	}

	/// <summary>
	/// Creates a branch plan fixture.
	/// </summary>
	/// <param name="branchDirectoryPath">Branch directory path.</param>
	/// <param name="links">Branch links.</param>
	/// <returns>Plan fixture.</returns>
	private static MergerfsBranchPlan CreatePlan(
		string branchDirectoryPath,
		IReadOnlyList<MergerfsBranchLinkDefinition> links)
	{
		return new MergerfsBranchPlan(
			preferredOverridePath: Path.Combine(branchDirectoryPath, "override"),
			branchDirectoryPath,
			"branch-spec",
			"suwayomi_hash",
			"group-id",
			links);
	}

	/// <summary>
	/// In-memory file-system fake for branch staging behavior.
	/// </summary>
	private sealed class RecordingBranchLinkStagingFileSystem : IBranchLinkStagingFileSystem
	{
		/// <summary>
		/// Existing directory set.
		/// </summary>
		private readonly HashSet<string> _directories = new(StringComparer.Ordinal);

		/// <summary>
		/// Existing file set.
		/// </summary>
		private readonly HashSet<string> _files = new(StringComparer.Ordinal);

		/// <summary>
		/// Existing link target map.
		/// </summary>
		private readonly Dictionary<string, string> _linkTargets = new(StringComparer.Ordinal);

		/// <summary>
		/// Gets created directory paths.
		/// </summary>
		public List<string> CreatedDirectories
		{
			get;
		} = [];

		/// <summary>
		/// Gets created symbolic links.
		/// </summary>
		public Dictionary<string, string> CreatedLinks
		{
			get;
		} = new(StringComparer.Ordinal);

		/// <summary>
		/// Gets deleted directory paths.
		/// </summary>
		public List<string> DeletedDirectories
		{
			get;
		} = [];

		/// <summary>
		/// Gets deleted file paths.
		/// </summary>
		public List<string> DeletedFiles
		{
			get;
		} = [];

		/// <summary>
		/// Configures one existing directory path.
		/// </summary>
		/// <param name="path">Directory path.</param>
		public void SetExistingDirectory(string path)
		{
			_directories.Add(path);
		}

		/// <summary>
		/// Configures one existing link.
		/// </summary>
		/// <param name="linkPath">Link path.</param>
		/// <param name="targetPath">Target path.</param>
		public void SetExistingLink(string linkPath, string targetPath)
		{
			_directories.Add(linkPath);
			_linkTargets[linkPath] = targetPath;
		}

		/// <summary>
		/// Configures one existing dangling directory symbolic link.
		/// </summary>
		/// <param name="linkPath">Link path.</param>
		/// <param name="targetPath">Link target path.</param>
		public void SetExistingDanglingLink(string linkPath, string targetPath)
		{
			_linkTargets[linkPath] = targetPath;
		}

		/// <inheritdoc />
		public bool DirectoryExists(string path)
		{
			return _directories.Contains(path);
		}

		/// <inheritdoc />
		public bool FileExists(string path)
		{
			return _files.Contains(path);
		}

		/// <inheritdoc />
		public bool PathExists(string path)
		{
			return _directories.Contains(path) || _files.Contains(path) || _linkTargets.ContainsKey(path);
		}

		/// <inheritdoc />
		public void CreateDirectory(string path)
		{
			CreatedDirectories.Add(path);
			_directories.Add(path);
		}

		/// <inheritdoc />
		public IEnumerable<string> EnumerateDirectories(string path)
		{
			string prefix = path + Path.DirectorySeparatorChar;
			return _directories.Where(directory => directory.StartsWith(prefix, StringComparison.Ordinal));
		}

		/// <inheritdoc />
		public IEnumerable<string> EnumerateFileSystemEntries(string path)
		{
			string prefix = path + Path.DirectorySeparatorChar;
			return _directories
				.Concat(_linkTargets.Keys)
				.Concat(_files)
				.Where(entry => entry.StartsWith(prefix, StringComparison.Ordinal))
				.Distinct(StringComparer.Ordinal)
				.ToArray();
		}

		/// <inheritdoc />
		public void DeleteDirectory(string path, bool recursive)
		{
			DeletedDirectories.Add(path);
			_directories.Remove(path);
			_linkTargets.Remove(path);
		}

		/// <inheritdoc />
		public void DeleteFile(string path)
		{
			DeletedFiles.Add(path);
			_files.Remove(path);
			_linkTargets.Remove(path);
		}

		/// <inheritdoc />
		public void CreateDirectorySymbolicLink(string linkPath, string targetPath)
		{
			CreatedLinks[linkPath] = targetPath;
			_directories.Add(linkPath);
			_linkTargets[linkPath] = targetPath;
		}

		/// <inheritdoc />
		public string? TryGetDirectoryLinkTarget(string linkPath)
		{
			return _linkTargets.TryGetValue(linkPath, out string? target)
				? target
				: null;
		}
	}
}
