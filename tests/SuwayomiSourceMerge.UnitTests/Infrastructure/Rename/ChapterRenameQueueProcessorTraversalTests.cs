namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Rename;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies recursive enqueue traversal methods for <see cref="ChapterRenameQueueProcessor"/>.
/// </summary>
public sealed class ChapterRenameQueueProcessorTraversalTests
{
	/// <summary>
	/// Verifies source-level traversal enqueues chapter directories under all manga children.
	/// </summary>
	[Fact]
	public void EnqueueChaptersUnderSourcePath_Expected_ShouldEnqueueNestedChapterDirectories()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		string sourcePath = Directory.CreateDirectory(Path.Combine(sourcesRootPath, "SourceA")).FullName;
		string mangaAPath = Directory.CreateDirectory(Path.Combine(sourcePath, "MangaA")).FullName;
		string mangaBPath = Directory.CreateDirectory(Path.Combine(sourcePath, "MangaB")).FullName;
		Directory.CreateDirectory(Path.Combine(mangaAPath, "Team9_Chapter 1"));
		Directory.CreateDirectory(Path.Combine(mangaBPath, "Team9_Chapter 2"));

		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueProcessor processor = CreateProcessor(sourcesRootPath, store, excludedSources: []);

		int enqueued = processor.EnqueueChaptersUnderSourcePath(sourcePath);

		Assert.Equal(2, enqueued);
		Assert.Equal(2, store.ReadAll().Count);
	}

	/// <summary>
	/// Verifies excluded source traversal is skipped.
	/// </summary>
	[Fact]
	public void EnqueueChaptersUnderSourcePath_Edge_ShouldReturnZero_WhenSourceExcluded()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		string sourcePath = Directory.CreateDirectory(Path.Combine(sourcesRootPath, "Local Source")).FullName;
		string mangaPath = Directory.CreateDirectory(Path.Combine(sourcePath, "MangaA")).FullName;
		Directory.CreateDirectory(Path.Combine(mangaPath, "Team9_Chapter 1"));

		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueProcessor processor = CreateProcessor(sourcesRootPath, store, excludedSources: ["Local Source"]);

		int enqueued = processor.EnqueueChaptersUnderSourcePath(sourcePath);

		Assert.Equal(0, enqueued);
		Assert.Empty(store.ReadAll());
	}

	/// <summary>
	/// Verifies invalid manga path input is rejected.
	/// </summary>
	[Fact]
	public void EnqueueChaptersUnderMangaPath_Failure_ShouldThrow_WhenInputPathIsInvalid()
	{
		ChapterRenameQueueProcessor processor = CreateProcessor("/ssm/sources", new InMemoryChapterRenameQueueStore(), excludedSources: []);

		Assert.ThrowsAny<ArgumentException>(() => processor.EnqueueChaptersUnderMangaPath(" "));
	}

	/// <summary>
	/// Creates a queue processor configured for traversal tests.
	/// </summary>
	/// <param name="sourcesRootPath">Sources root path.</param>
	/// <param name="queueStore">Queue store instance.</param>
	/// <param name="excludedSources">Excluded source names.</param>
	/// <returns>Configured processor.</returns>
	private static ChapterRenameQueueProcessor CreateProcessor(
		string sourcesRootPath,
		IChapterRenameQueueStore queueStore,
		IReadOnlyList<string> excludedSources)
	{
		ChapterRenameOptions options = new(
			sourcesRootPath,
			renameDelaySeconds: 5,
			renameQuietSeconds: 0,
			renamePollSeconds: 20,
			renameRescanSeconds: 600,
			excludedSources);

		return new ChapterRenameQueueProcessor(
			options,
			new ShellParityChapterRenameSanitizer(),
			queueStore,
			new ChapterRenameFileSystem(),
			new NullLogger());
	}

	/// <summary>
	/// Logger implementation that discards all messages.
	/// </summary>
	private sealed class NullLogger : ISsmLogger
	{
		/// <inheritdoc />
		public void Trace(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
		}

		/// <inheritdoc />
		public void Debug(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
		}

		/// <inheritdoc />
		public void Normal(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
		}

		/// <inheritdoc />
		public void Warning(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
		}

		/// <inheritdoc />
		public void Error(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
		}

		/// <inheritdoc />
		public void Log(LogLevel level, string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
		}

		/// <inheritdoc />
		public bool IsEnabled(LogLevel level)
		{
			return false;
		}
	}
}
