using System.Collections.ObjectModel;

using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Represents immutable persisted metadata orchestration state.
/// </summary>
internal sealed class MetadataStateSnapshot
{
	/// <summary>
	/// Shared empty snapshot instance.
	/// </summary>
	private static readonly MetadataStateSnapshot _empty = new(
		new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
		null,
		Array.Empty<ComickApiCacheEntry>());

	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataStateSnapshot"/> class.
	/// </summary>
	/// <param name="titleCooldownsUtc">Per-title cooldown timestamps keyed by normalized title key.</param>
	/// <param name="stickyFlaresolverrUntilUtc">Sticky FlareSolverr routing expiry timestamp, when present.</param>
	/// <param name="comickCache">Optional persisted Comick response cache entries.</param>
	public MetadataStateSnapshot(
		IReadOnlyDictionary<string, DateTimeOffset> titleCooldownsUtc,
		DateTimeOffset? stickyFlaresolverrUntilUtc,
		IReadOnlyCollection<ComickApiCacheEntry>? comickCache = null)
	{
		ArgumentNullException.ThrowIfNull(titleCooldownsUtc);

		Dictionary<string, DateTimeOffset> normalizedCooldowns = new(StringComparer.Ordinal);
		foreach (KeyValuePair<string, DateTimeOffset> entry in titleCooldownsUtc)
		{
			string? key = entry.Key;
			if (string.IsNullOrWhiteSpace(key))
			{
				throw new ArgumentException(
					"Title cooldown keys must not contain null, empty, or whitespace values.",
					nameof(titleCooldownsUtc));
			}

			string normalizedKey = key.Trim();
			DateTimeOffset normalizedTimestamp = entry.Value.ToUniversalTime();
			if (!normalizedCooldowns.TryAdd(normalizedKey, normalizedTimestamp))
			{
				throw new ArgumentException(
					$"Title cooldown keys must be unique after normalization. Duplicate key '{normalizedKey}'.",
					nameof(titleCooldownsUtc));
			}
		}

		TitleCooldownsUtc = new ReadOnlyDictionary<string, DateTimeOffset>(normalizedCooldowns);
		StickyFlaresolverrUntilUtc = stickyFlaresolverrUntilUtc?.ToUniversalTime();

		List<ComickApiCacheEntry> normalizedComickCache = [];
		if (comickCache is not null)
		{
			foreach (ComickApiCacheEntry entry in comickCache)
			{
				ArgumentNullException.ThrowIfNull(entry, nameof(comickCache));
				normalizedComickCache.Add(
					new ComickApiCacheEntry(
						entry.EndpointKind,
						entry.RequestKey,
						entry.Outcome,
						entry.StatusCode,
						entry.Diagnostic,
						entry.PayloadJson,
						entry.ExpiresAtUtc));
			}
		}

		ComickCache = new ReadOnlyCollection<ComickApiCacheEntry>(normalizedComickCache);
	}

	/// <summary>
	/// Gets an empty metadata state snapshot.
	/// </summary>
	public static MetadataStateSnapshot Empty
	{
		get
		{
			return _empty;
		}
	}

	/// <summary>
	/// Gets per-title cooldown timestamps keyed by normalized title key.
	/// </summary>
	public IReadOnlyDictionary<string, DateTimeOffset> TitleCooldownsUtc
	{
		get;
	}

	/// <summary>
	/// Gets sticky FlareSolverr routing expiry timestamp, when present.
	/// </summary>
	public DateTimeOffset? StickyFlaresolverrUntilUtc
	{
		get;
	}

	/// <summary>
	/// Gets persisted Comick API cache entries.
	/// </summary>
	public IReadOnlyCollection<ComickApiCacheEntry> ComickCache
	{
		get;
	}
}
