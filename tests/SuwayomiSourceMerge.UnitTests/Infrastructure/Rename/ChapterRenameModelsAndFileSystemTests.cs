namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Rename;

using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies chapter rename model and filesystem adapter behavior.
/// </summary>
public sealed class ChapterRenameModelsAndFileSystemTests
{
	/// <summary>
	/// Verifies queue entries normalize full paths.
	/// </summary>
	[Fact]
	public void ChapterRenameQueueEntry_Expected_ShouldNormalizePath()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string relativePath = Path.Combine(temporaryDirectory.Path, "a", "..", "chapter");

		ChapterRenameQueueEntry entry = new(10, relativePath);

		Assert.Equal(Path.GetFullPath(relativePath), entry.Path);
		Assert.Equal(10, entry.AllowAtUnixSeconds);
	}

	/// <summary>
	/// Verifies queue entries reject invalid paths.
	/// </summary>
	[Fact]
	public void ChapterRenameQueueEntry_Failure_ShouldThrow_WhenPathIsInvalid()
	{
		Assert.ThrowsAny<ArgumentException>(() => new ChapterRenameQueueEntry(0, ""));
		Assert.ThrowsAny<ArgumentException>(() => new ChapterRenameQueueEntry(0, " "));
		Assert.Throws<ArgumentNullException>(() => new ChapterRenameQueueEntry(0, null!));
	}

	/// <summary>
	/// Verifies process and rescan result models store constructor values.
	/// </summary>
	[Fact]
	public void ResultModels_Expected_ShouldStoreConstructorValues()
	{
		ChapterRenameProcessResult processResult = new(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
		ChapterRenameRescanResult rescanResult = new(11, 12);

		Assert.Equal(1, processResult.ProcessedEntries);
		Assert.Equal(2, processResult.RenamedEntries);
		Assert.Equal(3, processResult.UnchangedEntries);
		Assert.Equal(4, processResult.DeferredMissingEntries);
		Assert.Equal(5, processResult.DroppedMissingEntries);
		Assert.Equal(6, processResult.DeferredNotReadyEntries);
		Assert.Equal(7, processResult.DeferredNotQuietEntries);
		Assert.Equal(8, processResult.CollisionSkippedEntries);
		Assert.Equal(9, processResult.MoveFailedEntries);
		Assert.Equal(10, processResult.RemainingQueuedEntries);
		Assert.Equal(11, rescanResult.CandidateEntries);
		Assert.Equal(12, rescanResult.EnqueuedEntries);
	}

	/// <summary>
	/// Verifies filesystem adapter returns expected values for normal and failure paths.
	/// </summary>
	[Fact]
	public void ChapterRenameFileSystem_ExpectedAndEdge_ShouldHandleExistingAndMissingPaths()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourceDirectoryPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "source")).FullName;
		string chapterDirectoryPath = Directory.CreateDirectory(Path.Combine(sourceDirectoryPath, "chapter")).FullName;
		string filePath = Path.Combine(chapterDirectoryPath, "page1.jpg");
		File.WriteAllText(filePath, "x");
		ChapterRenameFileSystem fileSystem = new();

		Assert.True(fileSystem.DirectoryExists(sourceDirectoryPath));
		Assert.True(fileSystem.PathExists(filePath));
		Assert.NotEmpty(fileSystem.EnumerateDirectories(sourceDirectoryPath));
		Assert.NotEmpty(fileSystem.EnumerateFileSystemEntries(sourceDirectoryPath));
		Assert.True(fileSystem.TryGetLastWriteTimeUtc(filePath, out DateTimeOffset _));

		string movedPath = Path.Combine(sourceDirectoryPath, "chapter_moved");
		Assert.True(fileSystem.TryMoveDirectory(chapterDirectoryPath, movedPath));
		Assert.False(fileSystem.DirectoryExists(chapterDirectoryPath));
		Assert.True(fileSystem.DirectoryExists(movedPath));

		Assert.False(fileSystem.DirectoryExists(Path.Combine(sourceDirectoryPath, "missing")));
		Assert.False(fileSystem.PathExists(Path.Combine(sourceDirectoryPath, "missing.file")));
		Assert.Empty(fileSystem.EnumerateDirectories(Path.Combine(sourceDirectoryPath, "missing")));
		Assert.Empty(fileSystem.EnumerateFileSystemEntries(Path.Combine(sourceDirectoryPath, "missing")));
		Assert.False(fileSystem.TryGetLastWriteTimeUtc(Path.Combine(sourceDirectoryPath, "missing"), out DateTimeOffset _));
	}

	/// <summary>
	/// Verifies filesystem adapter guard clauses reject invalid arguments.
	/// </summary>
	[Fact]
	public void ChapterRenameFileSystem_Failure_ShouldThrow_WhenPathArgumentsInvalid()
	{
		ChapterRenameFileSystem fileSystem = new();

		Assert.ThrowsAny<ArgumentException>(() => fileSystem.GetFullPath(""));
		Assert.ThrowsAny<ArgumentException>(() => fileSystem.DirectoryExists(" "));
		Assert.ThrowsAny<ArgumentException>(() => fileSystem.PathExists(""));
		Assert.ThrowsAny<ArgumentException>(() => fileSystem.EnumerateDirectories(""));
		Assert.ThrowsAny<ArgumentException>(() => fileSystem.EnumerateFileSystemEntries(""));
		Assert.ThrowsAny<ArgumentException>(() => fileSystem.TryGetLastWriteTimeUtc("", out DateTimeOffset _));
		Assert.ThrowsAny<ArgumentException>(() => fileSystem.TryMoveDirectory("", "x"));
		Assert.ThrowsAny<ArgumentException>(() => fileSystem.TryMoveDirectory("x", ""));
	}
}

