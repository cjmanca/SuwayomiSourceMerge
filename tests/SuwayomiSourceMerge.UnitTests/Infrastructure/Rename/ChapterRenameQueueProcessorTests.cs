namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Rename;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies chapter rename queue processor behavior for enqueue, processing, and rescan flows.
/// </summary>
public sealed class ChapterRenameQueueProcessorTests
{
	/// <summary>
	/// Verifies valid depth-3 chapter paths are queued with delay-based allow-at timestamps.
	/// </summary>
	[Fact]
	public void EnqueueChapterPath_Expected_ShouldQueueValidDepthThreePathWithDelay()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		string chapterPath = CreateDirectory(sourcesRootPath, "SourceA", "MangaA", "Team9_Chapter 1");

		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			new ChapterRenameFileSystem(),
			new RecordingLogger(),
			renameDelaySeconds: 30,
			renameQuietSeconds: 0,
			renameRescanSeconds: 120);

		long before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		bool queued = processor.EnqueueChapterPath(chapterPath);
		long after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		ChapterRenameQueueEntry queuedEntry = Assert.Single(store.ReadAll());

		Assert.True(queued);
		Assert.InRange(queuedEntry.AllowAtUnixSeconds, before + 30, after + 30);
		Assert.Equal(Path.GetFullPath(chapterPath), queuedEntry.Path);
	}

	/// <summary>
	/// Verifies excluded sources, duplicate paths, and non-depth-3 paths are not enqueued.
	/// </summary>
	[Fact]
	public void EnqueueChapterPath_Edge_ShouldRejectExcludedDuplicateAndInvalidDepthPaths()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		string excludedChapterPath = CreateDirectory(sourcesRootPath, "Local Source", "MangaA", "Chapter 1");
		string validChapterPath = CreateDirectory(sourcesRootPath, "SourceA", "MangaA", "Chapter 1");
		string mangaPath = Path.Combine(sourcesRootPath, "SourceA", "MangaA");

		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			new ChapterRenameFileSystem(),
			new RecordingLogger(),
			excludedSources: ["Local Source"]);

		bool excludedQueued = processor.EnqueueChapterPath(excludedChapterPath);
		bool firstQueued = processor.EnqueueChapterPath(validChapterPath);
		bool duplicateQueued = processor.EnqueueChapterPath(validChapterPath);
		bool invalidDepthQueued = processor.EnqueueChapterPath(mangaPath);

		Assert.False(excludedQueued);
		Assert.True(firstQueued);
		Assert.False(duplicateQueued);
		Assert.False(invalidDepthQueued);
		Assert.Single(store.ReadAll());
	}

	/// <summary>
	/// Verifies null enqueue paths are rejected.
	/// </summary>
	[Fact]
	public void EnqueueChapterPath_Failure_ShouldThrow_WhenPathIsNull()
	{
		ChapterRenameQueueProcessor processor = CreateProcessor(
			"/ssm/sources",
			new InMemoryChapterRenameQueueStore(),
			new ChapterRenameFileSystem(),
			new RecordingLogger());

		Assert.ThrowsAny<ArgumentException>(() => processor.EnqueueChapterPath(null!));
	}

	/// <summary>
	/// Verifies eligible quiet entries are renamed and removed from queue.
	/// </summary>
	[Fact]
	public void ProcessOnce_Expected_ShouldRenameEligibleEntryAndRemoveItFromQueue()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		string chapterPath = CreateDirectory(sourcesRootPath, "SourceA", "MangaA", "Asura1 Chapter 7");
		string markerPath = Path.Combine(chapterPath, "page1.jpg");
		File.WriteAllText(markerPath, "x");
		DateTime quietTime = DateTime.UtcNow.AddMinutes(-10);
		File.SetLastWriteTimeUtc(markerPath, quietTime);
		Directory.SetLastWriteTimeUtc(chapterPath, quietTime);

		InMemoryChapterRenameQueueStore store = new();
		store.TryEnqueue(new ChapterRenameQueueEntry(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60, chapterPath));

		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			new ChapterRenameFileSystem(),
			new RecordingLogger(),
			renameDelaySeconds: 0,
			renameQuietSeconds: 60,
			renameRescanSeconds: 300);

		ChapterRenameProcessResult result = processor.ProcessOnce();

		Assert.Equal(1, result.RenamedEntries);
		Assert.Equal(0, result.RemainingQueuedEntries);
		Assert.True(Directory.Exists(Path.Combine(sourcesRootPath, "SourceA", "MangaA", "Asura Chapter 7")));
		Assert.False(Directory.Exists(chapterPath));
	}

	/// <summary>
	/// Verifies entries before allow-at timestamps stay queued.
	/// </summary>
	[Fact]
	public void ProcessOnce_Edge_ShouldDeferEntry_WhenAllowAtHasNotElapsed()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		string chapterPath = CreateDirectory(sourcesRootPath, "SourceA", "MangaA", "Asura1 Chapter 7");

		InMemoryChapterRenameQueueStore store = new();
		store.TryEnqueue(new ChapterRenameQueueEntry(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 300, chapterPath));
		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			new ChapterRenameFileSystem(),
			new RecordingLogger(),
			renameDelaySeconds: 0,
			renameQuietSeconds: 0,
			renameRescanSeconds: 300);

		ChapterRenameProcessResult result = processor.ProcessOnce();

		Assert.Equal(1, result.DeferredNotReadyEntries);
		Assert.Equal(1, result.RemainingQueuedEntries);
	}

	/// <summary>
	/// Verifies entries that are not quiet enough stay queued.
	/// </summary>
	[Fact]
	public void ProcessOnce_Edge_ShouldDeferEntry_WhenQuietWindowHasNotElapsed()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		string chapterPath = CreateDirectory(sourcesRootPath, "SourceA", "MangaA", "Asura1 Chapter 7");
		string markerPath = Path.Combine(chapterPath, "page1.jpg");
		File.WriteAllText(markerPath, "x");
		File.SetLastWriteTimeUtc(markerPath, DateTime.UtcNow);

		InMemoryChapterRenameQueueStore store = new();
		store.TryEnqueue(new ChapterRenameQueueEntry(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60, chapterPath));
		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			new ChapterRenameFileSystem(),
			new RecordingLogger(),
			renameDelaySeconds: 0,
			renameQuietSeconds: 120,
			renameRescanSeconds: 300);

		ChapterRenameProcessResult result = processor.ProcessOnce();

		Assert.Equal(1, result.DeferredNotQuietEntries);
		Assert.Equal(1, result.RemainingQueuedEntries);
	}

	/// <summary>
	/// Verifies quiet checks short-circuit once one recent child timestamp proves the entry is not quiet.
	/// </summary>
	[Fact]
	public void ProcessOnce_Edge_ShouldShortCircuitQuietCheck_WhenRecentChildIsFound()
	{
		string sourcesRootPath = Path.GetFullPath("/ssm/sources");
		string chapterPath = Path.Combine(sourcesRootPath, "SourceA", "MangaA", "Asura1 Chapter 7");
		string recentChildPath = Path.Combine(chapterPath, "recent.jpg");
		string olderChildPath = Path.Combine(chapterPath, "older.jpg");

		int childTimestampLookups = 0;
		FakeChapterRenameFileSystem fileSystem = new()
		{
			DirectoryExistsHandler = path => string.Equals(path, chapterPath, StringComparison.Ordinal),
			EnumerateFileSystemEntriesHandler = static _ => [],
			TryMoveDirectoryHandler = static (_, _) => false,
			PathExistsHandler = static _ => false
		};
		fileSystem.EnumerateFileSystemEntriesHandler = path =>
		{
			if (string.Equals(path, chapterPath, StringComparison.Ordinal))
			{
				return [recentChildPath, olderChildPath];
			}

			return [];
		};
		fileSystem.TryGetLastWriteTimeUtcHandler = path =>
		{
			if (string.Equals(path, recentChildPath, StringComparison.Ordinal))
			{
				Interlocked.Increment(ref childTimestampLookups);
				return (true, DateTimeOffset.UtcNow);
			}

			if (string.Equals(path, olderChildPath, StringComparison.Ordinal))
			{
				Interlocked.Increment(ref childTimestampLookups);
				return (true, DateTimeOffset.UtcNow.AddHours(-2));
			}

			return (true, DateTimeOffset.UtcNow.AddHours(-3));
		};

		InMemoryChapterRenameQueueStore store = new();
		store.TryEnqueue(new ChapterRenameQueueEntry(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60, chapterPath));
		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			fileSystem,
			new RecordingLogger(),
			renameDelaySeconds: 0,
			renameQuietSeconds: 120,
			renameRescanSeconds: 300);

		ChapterRenameProcessResult result = processor.ProcessOnce();

		Assert.Equal(1, result.DeferredNotQuietEntries);
		Assert.Equal(1, result.RemainingQueuedEntries);
		Assert.Equal(1, childTimestampLookups);
	}

	/// <summary>
	/// Verifies missing paths are kept in queue within grace windows and dropped after grace expiration.
	/// </summary>
	[Fact]
	public void ProcessOnce_Edge_ShouldRetainOrDropMissingPath_BasedOnGraceWindow()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		string missingRecentPath = Path.Combine(sourcesRootPath, "SourceA", "MangaA", "MissingRecent");
		string missingOldPath = Path.Combine(sourcesRootPath, "SourceA", "MangaA", "MissingOld");
		long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		InMemoryChapterRenameQueueStore store = new();
		store.TryEnqueue(new ChapterRenameQueueEntry(now - 5, missingRecentPath));
		store.TryEnqueue(new ChapterRenameQueueEntry(now - 1000, missingOldPath));

		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			new ChapterRenameFileSystem(),
			new RecordingLogger(),
			renameDelaySeconds: 0,
			renameQuietSeconds: 0,
			renameRescanSeconds: 60);

		ChapterRenameProcessResult result = processor.ProcessOnce();
		IReadOnlyList<ChapterRenameQueueEntry> remaining = store.ReadAll();

		Assert.Equal(1, result.DeferredMissingEntries);
		Assert.Equal(1, result.DroppedMissingEntries);
		Assert.Single(remaining);
		Assert.Equal(Path.GetFullPath(missingRecentPath), remaining[0].Path);
	}

	/// <summary>
	/// Verifies collision handling appends alternate suffixes.
	/// </summary>
	[Fact]
	public void ProcessOnce_Edge_ShouldUseAlternateSuffix_WhenPrimaryTargetCollides()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		string mangaPath = CreateDirectory(sourcesRootPath, "SourceA", "MangaA");
		string chapterPath = CreateDirectory(mangaPath, "Team9_Chapter 2");
		CreateDirectory(mangaPath, "Team_Chapter 2");

		InMemoryChapterRenameQueueStore store = new();
		store.TryEnqueue(new ChapterRenameQueueEntry(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60, chapterPath));
		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			new ChapterRenameFileSystem(),
			new RecordingLogger(),
			renameDelaySeconds: 0,
			renameQuietSeconds: 0,
			renameRescanSeconds: 60);

		ChapterRenameProcessResult result = processor.ProcessOnce();

		Assert.Equal(1, result.RenamedEntries);
		Assert.True(Directory.Exists(Path.Combine(mangaPath, "Team_Chapter 2_alt-a")));
	}

	/// <summary>
	/// Verifies move failures are handled without throwing and entries are not requeued.
	/// </summary>
	[Fact]
	public void ProcessOnce_Failure_ShouldHandleMoveFailureWithoutRequeue()
	{
		string chapterPath = "/ssm/sources/source/manga/Asura1 Chapter 7";
		long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		InMemoryChapterRenameQueueStore store = new();
		store.TryEnqueue(new ChapterRenameQueueEntry(now - 120, chapterPath));
		FakeChapterRenameFileSystem fileSystem = new()
		{
			DirectoryExistsHandler = static _ => true,
			PathExistsHandler = static _ => false,
			EnumerateFileSystemEntriesHandler = static _ => [],
			TryGetLastWriteTimeUtcHandler = static _ => (true, DateTimeOffset.UtcNow.AddMinutes(-20)),
			TryMoveDirectoryHandler = static (_, _) => false
		};

		ChapterRenameQueueProcessor processor = CreateProcessor(
			"/ssm/sources",
			store,
			fileSystem,
			new RecordingLogger(),
			renameDelaySeconds: 0,
			renameQuietSeconds: 0,
			renameRescanSeconds: 60);

		ChapterRenameProcessResult result = processor.ProcessOnce();

		Assert.Equal(1, result.MoveFailedEntries);
		Assert.Equal(0, result.RemainingQueuedEntries);
		Assert.Empty(store.ReadAll());
	}

	/// <summary>
	/// Verifies enqueue requests that race with processing are retained and not dropped.
	/// </summary>
	[Fact]
	public async Task ProcessOnce_Edge_ShouldRetainConcurrentlyEnqueuedEntry_WhenProcessingIsInFlight()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		string firstPath = Path.Combine(sourcesRootPath, "SourceA", "MangaA", "Asura1 Chapter 7");
		string secondPath = Path.Combine(sourcesRootPath, "SourceA", "MangaA", "Team9_Chapter 8");
		long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		InMemoryChapterRenameQueueStore store = new();
		store.TryEnqueue(new ChapterRenameQueueEntry(now - 120, firstPath));

		using ManualResetEventSlim moveStarted = new(false);
		using ManualResetEventSlim completeMove = new(false);
		FakeChapterRenameFileSystem fileSystem = new()
		{
			DirectoryExistsHandler = path => string.Equals(path, firstPath, StringComparison.Ordinal),
			PathExistsHandler = static _ => false,
			EnumerateFileSystemEntriesHandler = static _ => [],
			TryGetLastWriteTimeUtcHandler = static _ => (true, DateTimeOffset.UtcNow.AddMinutes(-20)),
			TryMoveDirectoryHandler = (_, _) =>
			{
				moveStarted.Set();
				return completeMove.Wait(TimeSpan.FromSeconds(5));
			}
		};

		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			fileSystem,
			new RecordingLogger(),
			renameDelaySeconds: 0,
			renameQuietSeconds: 0,
			renameRescanSeconds: 60);

		Task<ChapterRenameProcessResult> processTask = Task.Run(processor.ProcessOnce);
		Assert.True(moveStarted.Wait(TimeSpan.FromSeconds(5)));
		Task<bool> enqueueTask = Task.Run(() => processor.EnqueueChapterPath(secondPath));

		completeMove.Set();
		ChapterRenameProcessResult result = await processTask.WaitAsync(TimeSpan.FromSeconds(5));
		bool queued = await enqueueTask.WaitAsync(TimeSpan.FromSeconds(5));
		ChapterRenameQueueEntry remaining = Assert.Single(store.ReadAll());

		Assert.Equal(1, result.RenamedEntries);
		Assert.True(queued);
		Assert.Equal(Path.GetFullPath(secondPath), remaining.Path);
	}

	/// <summary>
	/// Verifies concurrent queue-processing passes serialize without double-processing one entry.
	/// </summary>
	[Fact]
	public async Task ProcessOnce_Edge_ShouldSerializeConcurrentCalls_WithoutDoubleProcessing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		string chapterPath = Path.Combine(sourcesRootPath, "SourceA", "MangaA", "Team9_Chapter 9");
		long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		InMemoryChapterRenameQueueStore store = new();
		store.TryEnqueue(new ChapterRenameQueueEntry(now - 120, chapterPath));

		using ManualResetEventSlim moveStarted = new(false);
		using ManualResetEventSlim allowMove = new(false);
		int moveCalls = 0;
		FakeChapterRenameFileSystem fileSystem = new()
		{
			DirectoryExistsHandler = path => string.Equals(path, chapterPath, StringComparison.Ordinal),
			PathExistsHandler = static _ => false,
			EnumerateFileSystemEntriesHandler = static _ => [],
			TryGetLastWriteTimeUtcHandler = static _ => (true, DateTimeOffset.UtcNow.AddMinutes(-20)),
			TryMoveDirectoryHandler = (_, _) =>
			{
				int count = Interlocked.Increment(ref moveCalls);
				if (count == 1)
				{
					moveStarted.Set();
					return allowMove.Wait(TimeSpan.FromSeconds(5));
				}

				return false;
			}
		};

		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			fileSystem,
			new RecordingLogger(),
			renameDelaySeconds: 0,
			renameQuietSeconds: 0,
			renameRescanSeconds: 60);

		Task<ChapterRenameProcessResult> firstPass = Task.Run(processor.ProcessOnce);
		Assert.True(moveStarted.Wait(TimeSpan.FromSeconds(5)));
		Task<ChapterRenameProcessResult> secondPass = Task.Run(processor.ProcessOnce);

		allowMove.Set();
		ChapterRenameProcessResult firstResult = await firstPass.WaitAsync(TimeSpan.FromSeconds(5));
		ChapterRenameProcessResult secondResult = await secondPass.WaitAsync(TimeSpan.FromSeconds(5));

		Assert.Equal(1, moveCalls);
		Assert.Equal(1, firstResult.RenamedEntries + secondResult.RenamedEntries);
		Assert.Equal(1, firstResult.ProcessedEntries + secondResult.ProcessedEntries);
		Assert.Empty(store.ReadAll());
	}

	/// <summary>
	/// Verifies rescans enqueue only chapter directories that require rename.
	/// </summary>
	[Fact]
	public void RescanAndEnqueue_Expected_ShouldQueueOnlyRenameCandidates()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		string mangaPath = CreateDirectory(sourcesRootPath, "SourceA", "MangaA");
		string candidatePath = CreateDirectory(mangaPath, "Team9_Chapter 1");
		CreateDirectory(mangaPath, "Chapter 2");
		DateTime oldTime = DateTime.UtcNow.AddMinutes(-20);
		Directory.SetLastWriteTimeUtc(candidatePath, oldTime);

		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			new ChapterRenameFileSystem(),
			new RecordingLogger(),
			renameDelaySeconds: 60,
			renameQuietSeconds: 0,
			renameRescanSeconds: 120);

		ChapterRenameRescanResult result = processor.RescanAndEnqueue();
		ChapterRenameQueueEntry queuedEntry = Assert.Single(store.ReadAll());

		Assert.Equal(1, result.CandidateEntries);
		Assert.Equal(1, result.EnqueuedEntries);
		Assert.Equal(Path.GetFullPath(candidatePath), queuedEntry.Path);
	}

	/// <summary>
	/// Verifies empty source roots yield no rescan candidates.
	/// </summary>
	[Fact]
	public void RescanAndEnqueue_Edge_ShouldReturnZero_WhenNoDirectoriesExist()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootPath = CreateDirectory(temporaryDirectory.Path, "sources");
		InMemoryChapterRenameQueueStore store = new();

		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			new ChapterRenameFileSystem(),
			new RecordingLogger());

		ChapterRenameRescanResult result = processor.RescanAndEnqueue();

		Assert.Equal(0, result.CandidateEntries);
		Assert.Equal(0, result.EnqueuedEntries);
		Assert.Empty(store.ReadAll());
	}

	/// <summary>
	/// Verifies rescan allow-at timestamps fall back to now-plus-delay when timestamps are unavailable.
	/// </summary>
	[Fact]
	public void RescanAndEnqueue_Edge_ShouldUseNowPlusDelay_WhenTimestampIsUnavailable()
	{
		string sourcesRootPath = Path.GetFullPath("/ssm/sources");
		string sourcePath = Path.Combine(sourcesRootPath, "SourceA");
		string mangaPath = Path.Combine(sourcePath, "MangaA");
		string chapterPath = Path.Combine(mangaPath, "Team9_Chapter 1");
		FakeChapterRenameFileSystem fileSystem = new()
		{
			EnumerateDirectoriesHandler = path =>
			{
				if (string.Equals(path, sourcesRootPath, StringComparison.Ordinal))
				{
					return [sourcePath];
				}

				if (string.Equals(path, sourcePath, StringComparison.Ordinal))
				{
					return [mangaPath];
				}

				if (string.Equals(path, mangaPath, StringComparison.Ordinal))
				{
					return [chapterPath];
				}

				return [];
			},
			TryGetLastWriteTimeUtcHandler = static _ => (false, default)
		};

		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			fileSystem,
			new RecordingLogger(),
			renameDelaySeconds: 30,
			renameQuietSeconds: 0,
			renameRescanSeconds: 60);

		long before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		ChapterRenameRescanResult result = processor.RescanAndEnqueue();
		long after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		ChapterRenameQueueEntry queuedEntry = Assert.Single(store.ReadAll());

		Assert.Equal(1, result.CandidateEntries);
		Assert.InRange(queuedEntry.AllowAtUnixSeconds, before + 30, after + 30);
	}

	/// <summary>
	/// Verifies rescans skip timestamp lookups for candidates already queued.
	/// </summary>
	[Fact]
	public void RescanAndEnqueue_Edge_ShouldSkipTimestampLookup_WhenCandidateIsAlreadyQueued()
	{
		string sourcesRootPath = Path.GetFullPath("/ssm/sources");
		string sourcePath = Path.Combine(sourcesRootPath, "SourceA");
		string mangaPath = Path.Combine(sourcePath, "MangaA");
		string chapterPath = Path.Combine(mangaPath, "Team9_Chapter 1");

		int timestampLookupCalls = 0;
		FakeChapterRenameFileSystem fileSystem = new()
		{
			EnumerateDirectoriesHandler = path =>
			{
				if (string.Equals(path, sourcesRootPath, StringComparison.Ordinal))
				{
					return [sourcePath];
				}

				if (string.Equals(path, sourcePath, StringComparison.Ordinal))
				{
					return [mangaPath];
				}

				if (string.Equals(path, mangaPath, StringComparison.Ordinal))
				{
					return [chapterPath];
				}

				return [];
			},
			TryGetLastWriteTimeUtcHandler = _ =>
			{
				Interlocked.Increment(ref timestampLookupCalls);
				return (true, DateTimeOffset.UtcNow.AddMinutes(-30));
			}
		};

		InMemoryChapterRenameQueueStore store = new();
		store.TryEnqueue(new ChapterRenameQueueEntry(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), chapterPath));

		ChapterRenameQueueProcessor processor = CreateProcessor(
			sourcesRootPath,
			store,
			fileSystem,
			new RecordingLogger(),
			renameDelaySeconds: 30,
			renameQuietSeconds: 0,
			renameRescanSeconds: 60);

		ChapterRenameRescanResult result = processor.RescanAndEnqueue();

		Assert.Equal(1, result.CandidateEntries);
		Assert.Equal(0, result.EnqueuedEntries);
		Assert.Equal(0, timestampLookupCalls);
		Assert.Single(store.ReadAll());
	}

	/// <summary>
	/// Verifies rescan treats directory-not-found races as benign without warning logs.
	/// </summary>
	[Fact]
	public void RescanAndEnqueue_Edge_ShouldSuppressDirectoryNotFoundFailuresWithoutWarning()
	{
		FakeChapterRenameFileSystem fileSystem = new()
		{
			EnumerateDirectoriesHandler = static _ => throw new DirectoryNotFoundException("gone")
		};
		RecordingLogger logger = new();
		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueProcessor processor = CreateProcessor(
			"/ssm/sources",
			store,
			fileSystem,
			logger);

		ChapterRenameRescanResult result = processor.RescanAndEnqueue();

		Assert.Equal(0, result.CandidateEntries);
		Assert.Equal(0, result.EnqueuedEntries);
		Assert.DoesNotContain(logger.Events, entry => entry.EventId == "rename.enumeration_warning");
	}

	/// <summary>
	/// Verifies rescan handles filesystem enumeration failures without throwing.
	/// </summary>
	[Fact]
	public void RescanAndEnqueue_Failure_ShouldHandleEnumerationFailuresBestEffort()
	{
		FakeChapterRenameFileSystem fileSystem = new()
		{
			EnumerateDirectoriesHandler = static _ => throw new IOException("boom")
		};
		RecordingLogger logger = new();
		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueProcessor processor = CreateProcessor(
			"/ssm/sources",
			store,
			fileSystem,
			logger);

		ChapterRenameRescanResult result = processor.RescanAndEnqueue();

		Assert.Equal(0, result.CandidateEntries);
		Assert.Equal(0, result.EnqueuedEntries);
		Assert.Contains(
			logger.Events,
			entry => entry.EventId == "rename.enumeration_warning" && entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Warning);
	}

	/// <summary>
	/// Creates one chapter rename queue processor with configurable options.
	/// </summary>
	/// <param name="sourcesRootPath">Sources root path.</param>
	/// <param name="queueStore">Queue store.</param>
	/// <param name="fileSystem">Filesystem adapter.</param>
	/// <param name="logger">Logger implementation.</param>
	/// <param name="renameDelaySeconds">Rename delay seconds.</param>
	/// <param name="renameQuietSeconds">Rename quiet seconds.</param>
	/// <param name="renameRescanSeconds">Rename rescan seconds.</param>
	/// <param name="excludedSources">Excluded source list.</param>
	/// <returns>Configured queue processor.</returns>
	private static ChapterRenameQueueProcessor CreateProcessor(
		string sourcesRootPath,
		IChapterRenameQueueStore queueStore,
		IChapterRenameFileSystem fileSystem,
		ISsmLogger logger,
		int renameDelaySeconds = 5,
		int renameQuietSeconds = 0,
		int renameRescanSeconds = 172800,
		IReadOnlyList<string>? excludedSources = null)
	{
		ChapterRenameOptions options = new(
			sourcesRootPath,
			renameDelaySeconds,
			renameQuietSeconds,
			renamePollSeconds: 20,
			renameRescanSeconds,
			excludedSources ?? []);

		return new ChapterRenameQueueProcessor(
			options,
			new ShellParityChapterRenameSanitizer(),
			queueStore,
			fileSystem,
			logger);
	}

	/// <summary>
	/// Creates one directory using a base path and relative segments.
	/// </summary>
	/// <param name="basePath">Base path.</param>
	/// <param name="segments">Relative path segments.</param>
	/// <returns>Created full directory path.</returns>
	private static string CreateDirectory(string basePath, params string[] segments)
	{
		string current = basePath;
		for (int index = 0; index < segments.Length; index++)
		{
			current = Path.Combine(current, segments[index]);
		}

		return Directory.CreateDirectory(current).FullName;
	}

	/// <summary>
	/// Minimal logger implementation for processor tests.
	/// </summary>
	private sealed class RecordingLogger : ISsmLogger
	{
		/// <summary>
		/// Captured log events.
		/// </summary>
		public List<(LogLevel Level, string EventId, string Message)> Events
		{
			get;
		} = [];

		/// <inheritdoc />
		public void Debug(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Events.Add((LogLevel.Debug, eventId, message));
		}

		/// <inheritdoc />
		public void Normal(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Events.Add((LogLevel.Normal, eventId, message));
		}

		/// <inheritdoc />
		public void Error(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Events.Add((LogLevel.Error, eventId, message));
		}

		/// <inheritdoc />
		public bool IsEnabled(LogLevel level)
		{
			return true;
		}

		/// <inheritdoc />
		public void Log(LogLevel level, string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Events.Add((level, eventId, message));
		}

		/// <inheritdoc />
		public void Trace(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Events.Add((LogLevel.Trace, eventId, message));
		}

		/// <inheritdoc />
		public void Warning(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Events.Add((LogLevel.Warning, eventId, message));
		}
	}

	/// <summary>
	/// Delegate-driven filesystem fake used by failure-path tests.
	/// </summary>
	private sealed class FakeChapterRenameFileSystem : IChapterRenameFileSystem
	{
		/// <summary>
		/// Full-path resolver override.
		/// </summary>
		public Func<string, string> GetFullPathHandler
		{
			get;
			set;
		} = static path => path;

		/// <summary>
		/// Directory existence handler.
		/// </summary>
		public Func<string, bool> DirectoryExistsHandler
		{
			get;
			set;
		} = static _ => true;

		/// <summary>
		/// Path existence handler.
		/// </summary>
		public Func<string, bool> PathExistsHandler
		{
			get;
			set;
		} = static _ => false;

		/// <summary>
		/// Directory enumeration handler.
		/// </summary>
		public Func<string, IEnumerable<string>> EnumerateDirectoriesHandler
		{
			get;
			set;
		} = static _ => [];

		/// <summary>
		/// Recursive entry enumeration handler.
		/// </summary>
		public Func<string, IEnumerable<string>> EnumerateFileSystemEntriesHandler
		{
			get;
			set;
		} = static _ => [];

		/// <summary>
		/// Last-write retrieval handler.
		/// </summary>
		public Func<string, (bool Success, DateTimeOffset Timestamp)> TryGetLastWriteTimeUtcHandler
		{
			get;
			set;
		} = static _ => (true, DateTimeOffset.UtcNow.AddMinutes(-20));

		/// <summary>
		/// Directory move handler.
		/// </summary>
		public Func<string, string, bool> TryMoveDirectoryHandler
		{
			get;
			set;
		} = static (_, _) => true;

		/// <inheritdoc />
		public bool DirectoryExists(string path)
		{
			return DirectoryExistsHandler(path);
		}

		/// <inheritdoc />
		public IEnumerable<string> EnumerateDirectories(string path)
		{
			return EnumerateDirectoriesHandler(path);
		}

		/// <inheritdoc />
		public IEnumerable<string> EnumerateFileSystemEntries(string path)
		{
			return EnumerateFileSystemEntriesHandler(path);
		}

		/// <inheritdoc />
		public string GetFullPath(string path)
		{
			return GetFullPathHandler(path);
		}

		/// <inheritdoc />
		public bool PathExists(string path)
		{
			return PathExistsHandler(path);
		}

		/// <inheritdoc />
		public bool TryGetLastWriteTimeUtc(string path, out DateTimeOffset lastWriteTimeUtc)
		{
			(bool success, DateTimeOffset timestamp) = TryGetLastWriteTimeUtcHandler(path);
			lastWriteTimeUtc = timestamp;
			return success;
		}

		/// <inheritdoc />
		public bool TryMoveDirectory(string sourcePath, string destinationPath)
		{
			return TryMoveDirectoryHandler(sourcePath, destinationPath);
		}
	}
}
