namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Rename;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies normal-level chapter rename queue summary logging behavior.
/// </summary>
public sealed class ChapterRenameQueueProcessorLoggingTests
{
	/// <summary>
	/// Event id emitted by rename queue processing summaries.
	/// </summary>
	private const string ProcessSummaryEvent = "rename.queue.processed";

	/// <summary>
	/// Verifies empty queue passes remain silent at normal log level.
	/// </summary>
	[Fact]
	public void ProcessOnce_Edge_ShouldSkipNormalSummary_WhenQueueIsEmpty()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		InMemoryChapterRenameQueueStore store = new();
		RecordingLogger logger = new();
		ChapterRenameQueueProcessor processor = CreateProcessor(sourcesRootPath, store, logger);

		ChapterRenameProcessResult result = processor.ProcessOnce();

		Assert.Equal(0, result.ProcessedEntries);
		Assert.DoesNotContain(
			logger.Events,
			entry => entry.Level == LogLevel.Normal && entry.EventId == ProcessSummaryEvent);
	}

	/// <summary>
	/// Verifies non-empty queue passes still emit normal-level processing summaries.
	/// </summary>
	[Fact]
	public void ProcessOnce_Expected_ShouldEmitNormalSummary_WhenQueueContainsEntries()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		string missingPath = Path.Combine(sourcesRootPath, "SourceA", "MangaA", "MissingChapter");
		long nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		InMemoryChapterRenameQueueStore store = new();
		store.TryEnqueue(new ChapterRenameQueueEntry(nowUnixSeconds - 30, missingPath));
		RecordingLogger logger = new();
		ChapterRenameQueueProcessor processor = CreateProcessor(sourcesRootPath, store, logger);

		ChapterRenameProcessResult result = processor.ProcessOnce();
		RecordingLogger.CapturedLogEvent summaryEvent = Assert.Single(
			logger.Events,
			entry => entry.Level == LogLevel.Normal && entry.EventId == ProcessSummaryEvent);

		Assert.Equal(1, result.ProcessedEntries);
		Assert.NotNull(summaryEvent.Context);
		Assert.Equal("1", summaryEvent.Context!["processed"]);
	}

	/// <summary>
	/// Creates one chapter rename queue processor with deterministic options.
	/// </summary>
	/// <param name="sourcesRootPath">Source root path.</param>
	/// <param name="store">Queue store dependency.</param>
	/// <param name="logger">Logger dependency.</param>
	/// <returns>Configured queue processor.</returns>
	private static ChapterRenameQueueProcessor CreateProcessor(
		string sourcesRootPath,
		IChapterRenameQueueStore store,
		RecordingLogger logger)
	{
		ChapterRenameOptions options = new(
			sourcesRootPath,
			renameDelaySeconds: 0,
			renameQuietSeconds: 0,
			renamePollSeconds: 20,
			renameRescanSeconds: 300,
			excludedSources: []);
		return new ChapterRenameQueueProcessor(
			options,
			new ShellParityChapterRenameSanitizer(),
			store,
			new ChapterRenameFileSystem(),
			logger);
	}

	/// <summary>
	/// Creates one directory using a base path and relative segments.
	/// </summary>
	/// <param name="basePath">Base path.</param>
	/// <param name="segments">Relative path segments.</param>
	/// <returns>Created full path.</returns>
	private static string CreateDirectory(string basePath, params string[] segments)
	{
		string current = basePath;
		for (int index = 0; index < segments.Length; index++)
		{
			current = Path.Combine(current, segments[index]);
		}

		return Directory.CreateDirectory(current).FullName;
	}
}
