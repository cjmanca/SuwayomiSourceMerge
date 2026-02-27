namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Expected-title list helper behavior for <see cref="ComickMetadataCoordinator"/>.
/// </summary>
internal sealed partial class ComickMetadataCoordinator
{
	/// <summary>
	/// Attempts to append one expected title while deduplicating by normalized title key.
	/// </summary>
	/// <param name="expectedTitles">Collected expected title values.</param>
	/// <param name="seenNormalizedKeys">Observed normalized title keys.</param>
	/// <param name="candidateTitle">Candidate title value.</param>
	/// <returns><see langword="true"/> when one title is appended; otherwise <see langword="false"/>.</returns>
	private bool TryAddExpectedTitle(
		ICollection<string> expectedTitles,
		ISet<string> seenNormalizedKeys,
		string? candidateTitle)
	{
		ArgumentNullException.ThrowIfNull(expectedTitles);
		ArgumentNullException.ThrowIfNull(seenNormalizedKeys);
		if (string.IsNullOrWhiteSpace(candidateTitle))
		{
			return false;
		}

		string trimmedTitle = candidateTitle.Trim();
		string normalizedTitleKey = _titleComparisonNormalizer.NormalizeTitleKey(trimmedTitle);
		if (string.IsNullOrWhiteSpace(normalizedTitleKey) || !seenNormalizedKeys.Add(normalizedTitleKey))
		{
			return false;
		}

		expectedTitles.Add(trimmedTitle);
		return true;
	}
}
