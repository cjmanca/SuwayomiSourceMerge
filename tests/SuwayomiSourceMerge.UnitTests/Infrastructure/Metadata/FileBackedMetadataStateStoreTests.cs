namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Text.Json;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="FileBackedMetadataStateStore"/>.
/// </summary>
public sealed class FileBackedMetadataStateStoreTests
{
	/// <summary>
	/// Verifies missing state files load deterministic empty defaults.
	/// </summary>
	[Fact]
	public void Constructor_Expected_ShouldInitializeEmptyState_WhenStateFileMissing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);
		FileBackedMetadataStateStore store = new(paths);

		MetadataStateSnapshot snapshot = store.Read();

		Assert.Empty(snapshot.TitleCooldownsUtc);
		Assert.Null(snapshot.StickyFlaresolverrUntilUtc);
		Assert.False(File.Exists(paths.MetadataStateFilePath));
		Assert.False(File.Exists(paths.MetadataStateCorruptFilePath));
		Assert.False(Directory.Exists(paths.MetadataStateCorruptDirectoryPath));
	}

	/// <summary>
	/// Verifies transformed state persists to disk and survives restart reload.
	/// </summary>
	[Fact]
	public void Transform_Expected_ShouldPersistAndReloadSnapshot()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);
		DateTimeOffset stickyUntil = DateTimeOffset.Parse("2026-02-22T13:00:00+00:00");
		DateTimeOffset titleCooldown = DateTimeOffset.Parse("2026-02-22T10:30:00+00:00");

		FileBackedMetadataStateStore store = new(paths);
		store.Transform(
			_ => new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
				{
					["title-key"] = titleCooldown
				},
				stickyUntil));

		FileBackedMetadataStateStore reloadedStore = new(paths);
		MetadataStateSnapshot reloadedSnapshot = reloadedStore.Read();

		Assert.True(File.Exists(paths.MetadataStateFilePath));
		Assert.Equal(stickyUntil, reloadedSnapshot.StickyFlaresolverrUntilUtc);
		Assert.Equal(titleCooldown, reloadedSnapshot.TitleCooldownsUtc["title-key"]);

		using JsonDocument writtenDocument = JsonDocument.Parse(File.ReadAllText(paths.MetadataStateFilePath));
		Assert.Equal(1, writtenDocument.RootElement.GetProperty("schema_version").GetInt32());
		Assert.Equal(
			stickyUntil.ToUnixTimeSeconds(),
			writtenDocument.RootElement.GetProperty("sticky_flaresolverr_until_unix_seconds").GetInt64());
		Assert.Equal(
			titleCooldown.ToUnixTimeSeconds(),
			writtenDocument.RootElement.GetProperty("title_cooldowns_unix_seconds").GetProperty("title-key").GetInt64());
	}

	/// <summary>
	/// Verifies multiple transforms preserve existing entries while applying new entries.
	/// </summary>
	[Fact]
	public void Transform_Edge_ShouldPreserveExistingCooldownsAcrossMultipleUpdates()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);
		FileBackedMetadataStateStore store = new(paths);
		DateTimeOffset first = DateTimeOffset.Parse("2026-02-22T11:00:00+00:00");
		DateTimeOffset second = DateTimeOffset.Parse("2026-02-22T12:00:00+00:00");

		store.Transform(
			_ => new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
				{
					["one"] = first
				},
				null));

		store.Transform(
			current =>
			{
				Dictionary<string, DateTimeOffset> nextCooldowns = new(current.TitleCooldownsUtc, StringComparer.Ordinal)
				{
					["two"] = second
				};
				return new MetadataStateSnapshot(nextCooldowns, current.StickyFlaresolverrUntilUtc);
			});

		MetadataStateSnapshot snapshot = store.Read();
		Assert.Equal(2, snapshot.TitleCooldownsUtc.Count);
		Assert.Equal(first, snapshot.TitleCooldownsUtc["one"]);
		Assert.Equal(second, snapshot.TitleCooldownsUtc["two"]);
	}

	/// <summary>
	/// Verifies read snapshots expose immutable cooldown data and do not mutate stored state.
	/// </summary>
	[Fact]
	public void Read_Edge_ShouldReturnDefensiveImmutableSnapshot()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);
		FileBackedMetadataStateStore store = new(paths);
		store.Transform(
			_ => new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
				{
					["immutable"] = DateTimeOffset.Parse("2026-02-22T11:30:00+00:00")
				},
				null));

		MetadataStateSnapshot snapshot = store.Read();
		IDictionary<string, DateTimeOffset> dictionaryView = Assert.IsAssignableFrom<IDictionary<string, DateTimeOffset>>(snapshot.TitleCooldownsUtc);
		Assert.Throws<NotSupportedException>(() => dictionaryView["new-key"] = DateTimeOffset.UtcNow);

		MetadataStateSnapshot rereadSnapshot = store.Read();
		Assert.Single(rereadSnapshot.TitleCooldownsUtc);
		Assert.DoesNotContain("new-key", rereadSnapshot.TitleCooldownsUtc.Keys);
	}

	/// <summary>
	/// Verifies directory-path corruption at metadata_state.json is quarantined and recovered.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldInitializeEmptyState_AfterRecoveringDirectoryStatePath()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);
		string corruptDirectoryPath = Directory.CreateDirectory(paths.MetadataStateFilePath).FullName;
		string markerPath = Path.Combine(corruptDirectoryPath, "marker.txt");
		File.WriteAllText(markerPath, "corrupt directory marker");

		FileBackedMetadataStateStore store = new(paths);
		MetadataStateSnapshot snapshot = store.Read();

		Assert.Empty(snapshot.TitleCooldownsUtc);
		Assert.Null(snapshot.StickyFlaresolverrUntilUtc);
		Assert.False(Directory.Exists(paths.MetadataStateFilePath));
		Assert.True(Directory.Exists(paths.MetadataStateCorruptDirectoryPath));
		Assert.True(File.Exists(Path.Combine(paths.MetadataStateCorruptDirectoryPath, "marker.txt")));
	}

	/// <summary>
	/// Verifies stale directory backups are replaced when recovering a new corrupt directory.
	/// </summary>
	[Fact]
	public void Constructor_Edge_ShouldReplaceStaleCorruptDirectoryBackup()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);

		string staleBackupPath = Directory.CreateDirectory(paths.MetadataStateCorruptDirectoryPath).FullName;
		File.WriteAllText(Path.Combine(staleBackupPath, "stale.txt"), "stale");

		string corruptDirectoryPath = Directory.CreateDirectory(paths.MetadataStateFilePath).FullName;
		File.WriteAllText(Path.Combine(corruptDirectoryPath, "fresh.txt"), "fresh");

		FileBackedMetadataStateStore store = new(paths);
		MetadataStateSnapshot snapshot = store.Read();

		Assert.Empty(snapshot.TitleCooldownsUtc);
		Assert.True(Directory.Exists(paths.MetadataStateCorruptDirectoryPath));
		Assert.True(File.Exists(Path.Combine(paths.MetadataStateCorruptDirectoryPath, "fresh.txt")));
		Assert.False(File.Exists(Path.Combine(paths.MetadataStateCorruptDirectoryPath, "stale.txt")));
	}

	/// <summary>
	/// Verifies directory quarantine fallback removes the corrupt source directory when backup move fails.
	/// </summary>
	[Fact]
	public void Constructor_Edge_ShouldDeleteCorruptDirectory_WhenBackupMoveFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);

		string corruptDirectoryPath = Directory.CreateDirectory(paths.MetadataStateFilePath).FullName;
		File.WriteAllText(Path.Combine(corruptDirectoryPath, "marker.txt"), "corrupt");

		Directory.CreateDirectory(paths.StateRootPath);
		File.WriteAllText(paths.MetadataStateCorruptDirectoryPath, "conflicting file prevents directory move");

		FileBackedMetadataStateStore store = new(paths);
		MetadataStateSnapshot snapshot = store.Read();

		Assert.Empty(snapshot.TitleCooldownsUtc);
		Assert.False(Directory.Exists(paths.MetadataStateFilePath));
		Assert.True(File.Exists(paths.MetadataStateCorruptDirectoryPath));
	}

	/// <summary>
	/// Verifies transform can write a valid metadata state file after directory-corruption recovery.
	/// </summary>
	[Fact]
	public void Transform_Expected_ShouldWriteStateFile_AfterDirectoryRecovery()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);

		Directory.CreateDirectory(paths.MetadataStateFilePath);
		FileBackedMetadataStateStore store = new(paths);

		DateTimeOffset cooldown = DateTimeOffset.Parse("2026-02-22T14:00:00+00:00");
		store.Transform(
			_ => new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
				{
					["title-after-recovery"] = cooldown
				},
				null));

		Assert.True(File.Exists(paths.MetadataStateFilePath));
		Assert.False(Directory.Exists(paths.MetadataStateFilePath));
		using JsonDocument writtenDocument = JsonDocument.Parse(File.ReadAllText(paths.MetadataStateFilePath));
		Assert.Equal(
			cooldown.ToUnixTimeSeconds(),
			writtenDocument.RootElement
				.GetProperty("title_cooldowns_unix_seconds")
				.GetProperty("title-after-recovery")
				.GetInt64());
	}

	/// <summary>
	/// Verifies malformed JSON triggers backup-and-reset recovery behavior.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldInitializeEmptyState_AfterRecoveringMalformedStateFile()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);
		Directory.CreateDirectory(paths.StateRootPath);
		string malformedContent = "{";
		File.WriteAllText(paths.MetadataStateFilePath, malformedContent);

		FileBackedMetadataStateStore store = new(paths);
		MetadataStateSnapshot snapshot = store.Read();

		Assert.Empty(snapshot.TitleCooldownsUtc);
		Assert.Null(snapshot.StickyFlaresolverrUntilUtc);
		Assert.False(File.Exists(paths.MetadataStateFilePath));
		Assert.True(File.Exists(paths.MetadataStateCorruptFilePath));
		Assert.Equal(malformedContent, File.ReadAllText(paths.MetadataStateCorruptFilePath));
	}

	/// <summary>
	/// Verifies unsupported schema versions are treated as corrupt and recovered.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldInitializeEmptyState_AfterRecoveringUnsupportedSchemaVersion()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);
		Directory.CreateDirectory(paths.StateRootPath);
		File.WriteAllText(
			paths.MetadataStateFilePath,
			"""
			{
			  "schema_version": 2,
			  "sticky_flaresolverr_until_unix_seconds": null,
			  "title_cooldowns_unix_seconds": {}
			}
			""");

		FileBackedMetadataStateStore store = new(paths);
		MetadataStateSnapshot snapshot = store.Read();

		Assert.Empty(snapshot.TitleCooldownsUtc);
		Assert.True(File.Exists(paths.MetadataStateCorruptFilePath));
	}

	/// <summary>
	/// Verifies invalid cooldown-map shapes are treated as corrupt and recovered.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldInitializeEmptyState_AfterRecoveringInvalidCooldownMapShape()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);
		Directory.CreateDirectory(paths.StateRootPath);
		File.WriteAllText(
			paths.MetadataStateFilePath,
			"""
			{
			  "schema_version": 1,
			  "sticky_flaresolverr_until_unix_seconds": null,
			  "title_cooldowns_unix_seconds": []
			}
			""");

		FileBackedMetadataStateStore store = new(paths);
		MetadataStateSnapshot snapshot = store.Read();

		Assert.Empty(snapshot.TitleCooldownsUtc);
		Assert.True(File.Exists(paths.MetadataStateCorruptFilePath));
	}

	/// <summary>
	/// Verifies invalid cooldown value types are treated as corrupt and recovered.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldInitializeEmptyState_AfterRecoveringInvalidCooldownValueType()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);
		Directory.CreateDirectory(paths.StateRootPath);
		File.WriteAllText(
			paths.MetadataStateFilePath,
			"""
			{
			  "schema_version": 1,
			  "sticky_flaresolverr_until_unix_seconds": null,
			  "title_cooldowns_unix_seconds": {
			    "title-key": "bad-value"
			  }
			}
			""");

		FileBackedMetadataStateStore store = new(paths);
		MetadataStateSnapshot snapshot = store.Read();

		Assert.Empty(snapshot.TitleCooldownsUtc);
		Assert.True(File.Exists(paths.MetadataStateCorruptFilePath));
	}

	/// <summary>
	/// Verifies transform rejects null callbacks.
	/// </summary>
	[Fact]
	public void Transform_Failure_ShouldThrow_WhenTransformerNull()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);
		FileBackedMetadataStateStore store = new(paths);

		Assert.Throws<ArgumentNullException>(() => store.Transform(null!));
	}

	/// <summary>
	/// Verifies transform rejects null callback return values.
	/// </summary>
	[Fact]
	public void Transform_Failure_ShouldThrow_WhenTransformerReturnsNull()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);
		FileBackedMetadataStateStore store = new(paths);

		ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => store.Transform(_ => null!));
		Assert.Equal("transformer", exception.ParamName);
	}

	/// <summary>
	/// Verifies persist failures do not swap the in-memory snapshot.
	/// </summary>
	[Fact]
	public void Transform_Failure_ShouldNotSwapInMemorySnapshot_WhenPersistFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		MetadataStatePaths paths = CreatePaths(temporaryDirectory.Path);
		FileBackedMetadataStateStore store = new(paths);
		DateTimeOffset firstCooldown = DateTimeOffset.Parse("2026-02-22T11:00:00+00:00");
		DateTimeOffset secondCooldown = DateTimeOffset.Parse("2026-02-22T12:00:00+00:00");

		store.Transform(
			_ => new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
				{
					["first"] = firstCooldown
				},
				null));

		File.Delete(paths.MetadataStateFilePath);
		Directory.CreateDirectory(paths.MetadataStateFilePath);

		Exception exception = Assert.ThrowsAny<Exception>(
			() => store.Transform(
				current =>
				{
					Dictionary<string, DateTimeOffset> nextCooldowns = new(current.TitleCooldownsUtc, StringComparer.Ordinal)
					{
						["second"] = secondCooldown
					};
					return new MetadataStateSnapshot(nextCooldowns, current.StickyFlaresolverrUntilUtc);
				}));
		Assert.True(exception is IOException or UnauthorizedAccessException);

		MetadataStateSnapshot snapshot = store.Read();
		Assert.Single(snapshot.TitleCooldownsUtc);
		Assert.Equal(firstCooldown, snapshot.TitleCooldownsUtc["first"]);
		Assert.DoesNotContain("second", snapshot.TitleCooldownsUtc.Keys);
	}

	/// <summary>
	/// Creates metadata state paths under one temporary root.
	/// </summary>
	/// <param name="rootPath">Temporary root path.</param>
	/// <returns>Initialized metadata state paths.</returns>
	private static MetadataStatePaths CreatePaths(string rootPath)
	{
		string stateRootPath = Path.Combine(rootPath, "state");
		return new MetadataStatePaths(stateRootPath);
	}
}
