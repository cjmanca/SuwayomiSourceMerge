using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Domain.Normalization;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Resolves source priority values from the configured source-priority list.
/// </summary>
/// <remarks>
/// Matching is normalized-only, using token normalization that ignores casing and punctuation differences.
/// </remarks>
internal sealed class SourcePriorityService : ISourcePriorityService
{
	/// <summary>
	/// Lookup of normalized source keys to configured priority indexes.
	/// </summary>
	private readonly IReadOnlyDictionary<string, int> _priorityBySourceKey;

	/// <summary>
	/// Initializes a new instance of the <see cref="SourcePriorityService"/> class.
	/// </summary>
	/// <param name="document">Parsed and validated source-priority document.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when document content is incomplete, normalizes to empty keys, or contains duplicate normalized sources.
	/// </exception>
	public SourcePriorityService(SourcePriorityDocument document)
	{
		ArgumentNullException.ThrowIfNull(document);

		_priorityBySourceKey = BuildLookup(document);
	}

	/// <inheritdoc />
	public bool TryGetPriority(string sourceName, out int priority)
	{
		ArgumentNullException.ThrowIfNull(sourceName);

		string normalizedKey = TitleKeyNormalizer.NormalizeTokenKey(sourceName);
		if (string.IsNullOrEmpty(normalizedKey))
		{
			priority = int.MaxValue;
			return false;
		}

		if (_priorityBySourceKey.TryGetValue(normalizedKey, out int foundPriority))
		{
			priority = foundPriority;
			return true;
		}

		priority = int.MaxValue;
		return false;
	}

	/// <inheritdoc />
	public int GetPriorityOrDefault(string sourceName, int unknownPriority = int.MaxValue)
	{
		ArgumentNullException.ThrowIfNull(sourceName);

		return TryGetPriority(sourceName, out int priority)
			? priority
			: unknownPriority;
	}

	/// <summary>
	/// Builds the normalized source-priority lookup from the document source list.
	/// </summary>
	/// <param name="document">Document to index.</param>
	/// <returns>Immutable lookup from normalized source keys to configured priority indexes.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the document source list is missing, contains empty items, or contains duplicate normalized entries.
	/// </exception>
	private static IReadOnlyDictionary<string, int> BuildLookup(SourcePriorityDocument document)
	{
		if (document.Sources is null)
		{
			throw new InvalidOperationException("Source priority document requires a non-null sources list.");
		}

		Dictionary<string, int> lookup = new(StringComparer.Ordinal);

		for (int index = 0; index < document.Sources.Count; index++)
		{
			string? sourceName = document.Sources[index];
			if (string.IsNullOrWhiteSpace(sourceName))
			{
				throw new InvalidOperationException(
					$"Source priority entry at index {index} is empty.");
			}

			string normalizedKey = TitleKeyNormalizer.NormalizeTokenKey(sourceName);
			if (string.IsNullOrEmpty(normalizedKey))
			{
				throw new InvalidOperationException(
					$"Source priority entry at index {index} becomes empty after normalization.");
			}

			if (!lookup.TryAdd(normalizedKey, index))
			{
				throw new InvalidOperationException(
					$"Source priority entry at index {index} duplicates an existing normalized source key '{normalizedKey}'.");
			}
		}

		return lookup;
	}
}
