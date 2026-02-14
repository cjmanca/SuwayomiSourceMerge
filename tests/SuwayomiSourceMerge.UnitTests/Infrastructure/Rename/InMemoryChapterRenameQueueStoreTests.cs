namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Rename;

using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies in-memory chapter rename queue storage behavior.
/// </summary>
public sealed class InMemoryChapterRenameQueueStoreTests
{
	/// <summary>
	/// Verifies enqueue operations preserve insertion order and track count.
	/// </summary>
	[Fact]
	public void TryEnqueue_Expected_ShouldAddEntryAndPreserveOrder()
	{
		using TemporaryDirectory temporaryDirectory = new();
		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueEntry first = new(10, Path.Combine(temporaryDirectory.Path, "a"));
		ChapterRenameQueueEntry second = new(20, Path.Combine(temporaryDirectory.Path, "b"));

		bool firstQueued = store.TryEnqueue(first);
		bool secondQueued = store.TryEnqueue(second);
		IReadOnlyList<ChapterRenameQueueEntry> snapshot = store.ReadAll();

		Assert.True(firstQueued);
		Assert.True(secondQueued);
		Assert.Equal(2, store.Count);
		Assert.Equal(first.Path, snapshot[0].Path);
		Assert.Equal(second.Path, snapshot[1].Path);
	}

	/// <summary>
	/// Verifies duplicate path enqueue requests are ignored.
	/// </summary>
	[Fact]
	public void TryEnqueue_Edge_ShouldIgnoreDuplicatePath()
	{
		using TemporaryDirectory temporaryDirectory = new();
		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueEntry first = new(10, Path.Combine(temporaryDirectory.Path, "a"));
		ChapterRenameQueueEntry duplicate = new(99, Path.Combine(temporaryDirectory.Path, "a"));

		store.TryEnqueue(first);
		bool duplicateQueued = store.TryEnqueue(duplicate);
		IReadOnlyList<ChapterRenameQueueEntry> snapshot = store.ReadAll();

		Assert.False(duplicateQueued);
		Assert.Single(snapshot);
		Assert.Equal(10, snapshot[0].AllowAtUnixSeconds);
	}

	/// <summary>
	/// Verifies transform replaces queue contents and de-duplicates by first-seen path.
	/// </summary>
	[Fact]
	public void Transform_Expected_ShouldReplaceQueueWithFirstSeenPathEntries()
	{
		using TemporaryDirectory temporaryDirectory = new();
		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueEntry first = new(1, Path.Combine(temporaryDirectory.Path, "x"));
		ChapterRenameQueueEntry duplicate = new(2, Path.Combine(temporaryDirectory.Path, "x"));
		ChapterRenameQueueEntry second = new(3, Path.Combine(temporaryDirectory.Path, "y"));

		store.Transform(_ => [first, duplicate, second]);
		IReadOnlyList<ChapterRenameQueueEntry> snapshot = store.ReadAll();

		Assert.Equal(2, snapshot.Count);
		Assert.Equal(first.Path, snapshot[0].Path);
		Assert.Equal(1, snapshot[0].AllowAtUnixSeconds);
		Assert.Equal(second.Path, snapshot[1].Path);
	}

	/// <summary>
	/// Verifies transformation callbacks receive queue snapshots in insertion order.
	/// </summary>
	[Fact]
	public void Transform_Edge_ShouldReceiveDeterministicSnapshot()
	{
		using TemporaryDirectory temporaryDirectory = new();
		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueEntry first = new(1, Path.Combine(temporaryDirectory.Path, "a"));
		ChapterRenameQueueEntry second = new(2, Path.Combine(temporaryDirectory.Path, "b"));
		store.TryEnqueue(first);
		store.TryEnqueue(second);

		List<string> paths = [];
		store.Transform(snapshot =>
		{
			for (int index = 0; index < snapshot.Count; index++)
			{
				paths.Add(snapshot[index].Path);
			}

			return snapshot;
		});

		Assert.Equal([first.Path, second.Path], paths);
	}

	/// <summary>
	/// Verifies null arguments and invalid transformation results are rejected.
	/// </summary>
	[Fact]
	public void Store_Failure_ShouldThrow_WhenArgumentsAreInvalid()
	{
		using TemporaryDirectory temporaryDirectory = new();
		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueEntry entry = new(1, Path.Combine(temporaryDirectory.Path, "a"));
		store.TryEnqueue(entry);

		Assert.Throws<ArgumentNullException>(() => store.TryEnqueue(null!));
		Assert.Throws<ArgumentNullException>(() => store.Transform(null!));
		Assert.Throws<ArgumentNullException>(() => store.Transform(static _ => null!));
	}

	/// <summary>
	/// Verifies queue state remains unchanged when transformation output contains null entries.
	/// </summary>
	[Fact]
	public void Transform_Failure_ShouldKeepOriginalQueue_WhenReplacementContainsNullEntry()
	{
		using TemporaryDirectory temporaryDirectory = new();
		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueEntry original = new(1, Path.Combine(temporaryDirectory.Path, "a"));
		store.TryEnqueue(original);

		Assert.Throws<ArgumentNullException>(() => store.Transform(static _ => [null!]));

		ChapterRenameQueueEntry remaining = Assert.Single(store.ReadAll());
		Assert.Equal(original.Path, remaining.Path);
		Assert.Equal(original.AllowAtUnixSeconds, remaining.AllowAtUnixSeconds);
	}

	/// <summary>
	/// Verifies queue state remains unchanged when transformation callbacks throw.
	/// </summary>
	[Fact]
	public void Transform_Failure_ShouldKeepOriginalQueue_WhenTransformerThrows()
	{
		using TemporaryDirectory temporaryDirectory = new();
		InMemoryChapterRenameQueueStore store = new();
		ChapterRenameQueueEntry original = new(1, Path.Combine(temporaryDirectory.Path, "a"));
		store.TryEnqueue(original);

		Assert.Throws<InvalidOperationException>(() => store.Transform(static _ => throw new InvalidOperationException("boom")));

		ChapterRenameQueueEntry remaining = Assert.Single(store.ReadAll());
		Assert.Equal(original.Path, remaining.Path);
		Assert.Equal(original.AllowAtUnixSeconds, remaining.AllowAtUnixSeconds);
	}
}
