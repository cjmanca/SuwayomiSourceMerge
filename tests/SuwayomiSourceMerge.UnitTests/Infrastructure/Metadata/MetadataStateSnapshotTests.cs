namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Text.Json;

using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="MetadataStateSnapshot"/>.
/// </summary>
public sealed class MetadataStateSnapshotTests
{
	/// <summary>
	/// Verifies constructor normalizes timestamps to UTC and exposes immutable cooldown map data.
	/// </summary>
	[Fact]
	public void Constructor_Expected_ShouldNormalizeUtcValuesAndExposeImmutableCooldownMap()
	{
		DateTimeOffset localOffsetTimestamp = new(2026, 2, 22, 13, 30, 0, TimeSpan.FromHours(2));
		MetadataStateSnapshot snapshot = new(
			new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
			{
				["title-key"] = localOffsetTimestamp
			},
			new DateTimeOffset(2026, 2, 22, 15, 0, 0, TimeSpan.FromHours(1)));

		Assert.Equal(localOffsetTimestamp.ToUniversalTime(), snapshot.TitleCooldownsUtc["title-key"]);
		Assert.Equal(
			new DateTimeOffset(2026, 2, 22, 15, 0, 0, TimeSpan.FromHours(1)).ToUniversalTime(),
			snapshot.StickyFlaresolverrUntilUtc);

		IDictionary<string, DateTimeOffset> dictionaryView = Assert.IsAssignableFrom<IDictionary<string, DateTimeOffset>>(snapshot.TitleCooldownsUtc);
		Assert.Throws<NotSupportedException>(() => dictionaryView["another-title"] = DateTimeOffset.UtcNow);
	}

	/// <summary>
	/// Verifies constructor allows null sticky-routing expiry values.
	/// </summary>
	[Fact]
	public void Constructor_Edge_ShouldAllowNullStickyFlaresolverrUntilUtc()
	{
		MetadataStateSnapshot snapshot = new(
			new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
			null);

		Assert.Null(snapshot.StickyFlaresolverrUntilUtc);
		Assert.Empty(snapshot.TitleCooldownsUtc);
	}

	/// <summary>
	/// Verifies constructor clones Comick cache entries and payload JSON.
	/// </summary>
	[Fact]
	public void Constructor_Edge_ShouldCloneComickCacheEntries()
	{
		JsonElement payloadJson = JsonSerializer.SerializeToElement(new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["slug"] = "cached-slug"
		});
		List<ComickApiCacheEntry> cacheEntries =
		[
			new ComickApiCacheEntry(
				ComickApiCacheEndpointKind.Search,
				"query",
				ComickDirectApiOutcome.Success,
				statusCode: 200,
				diagnostic: "cached",
				payloadJson,
				DateTimeOffset.Parse("2026-03-01T00:00:00+00:00"))
		];

		MetadataStateSnapshot snapshot = new(
			new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
			null,
			cacheEntries);
		cacheEntries.Clear();

		ComickApiCacheEntry cachedEntry = Assert.Single(snapshot.ComickCache);
		Assert.Equal("query", cachedEntry.RequestKey);
		Assert.Equal(ComickDirectApiOutcome.Success, cachedEntry.Outcome);
		Assert.True(cachedEntry.PayloadJson.HasValue);
	}

	/// <summary>
	/// Verifies constructor rejects null cooldown dictionaries.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenTitleCooldownsNull()
	{
		Assert.Throws<ArgumentNullException>(() => new MetadataStateSnapshot(null!, null));
	}

	/// <summary>
	/// Verifies constructor rejects invalid cooldown keys.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenTitleCooldownKeysInvalid()
	{
		Assert.Throws<ArgumentException>(
			() => new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
				{
					[" "] = DateTimeOffset.UtcNow
				},
				null));

		Assert.Throws<ArgumentException>(
			() => new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
				{
					["title-key"] = DateTimeOffset.UtcNow,
					[" title-key "] = DateTimeOffset.UtcNow
				},
				null));
	}
}
