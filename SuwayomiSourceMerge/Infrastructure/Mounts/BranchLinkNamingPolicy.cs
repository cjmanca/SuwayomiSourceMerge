using System.Buffers;

namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Produces deterministic filesystem-safe branch-link names for override and source branches.
/// </summary>
internal sealed class BranchLinkNamingPolicy
{
	/// <summary>
	/// Link name used for the primary read-write override branch.
	/// </summary>
	private const string PRIMARY_OVERRIDE_LINK_NAME = "00_override_primary";

	/// <summary>
	/// Prefix used for additional read-write override branches.
	/// </summary>
	private const string EXTRA_OVERRIDE_PREFIX = "01_override";

	/// <summary>
	/// Prefix used for read-only source branches.
	/// </summary>
	private const string SOURCE_PREFIX = "10_source";

	/// <summary>
	/// Placeholder label used when sanitization produces an empty value.
	/// </summary>
	private const string EMPTY_LABEL_FALLBACK = "x";

	/// <summary>
	/// Maximum label length processed on the stack before falling back to pooled buffers.
	/// </summary>
	private const int STACKALLOC_SANITIZE_THRESHOLD = 256;

	/// <summary>
	/// Builds the deterministic primary override link name.
	/// </summary>
	/// <returns>Primary override link name.</returns>
	public string BuildPrimaryOverrideLinkName()
	{
		return PRIMARY_OVERRIDE_LINK_NAME;
	}

	/// <summary>
	/// Builds the deterministic additional override link name.
	/// </summary>
	/// <param name="overrideVolumeRootPath">Absolute override volume root path used to derive the label.</param>
	/// <param name="index">Zero-based additional override index.</param>
	/// <returns>Deterministic additional override link name.</returns>
	public string BuildAdditionalOverrideLinkName(string overrideVolumeRootPath, int index)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(overrideVolumeRootPath);
		ArgumentOutOfRangeException.ThrowIfNegative(index);

		string volumeLabel = Path.GetFileName(Path.TrimEndingDirectorySeparator(overrideVolumeRootPath.Trim()));
		string sanitizedLabel = SanitizeSegment(volumeLabel);
		return $"{EXTRA_OVERRIDE_PREFIX}_{sanitizedLabel}_{index:000}";
	}

	/// <summary>
	/// Builds the deterministic source branch link name.
	/// </summary>
	/// <param name="sourceName">Source name used to derive the link label.</param>
	/// <param name="index">Zero-based source branch index.</param>
	/// <returns>Deterministic source branch link name.</returns>
	public string BuildSourceLinkName(string sourceName, int index)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
		ArgumentOutOfRangeException.ThrowIfNegative(index);

		string sanitizedLabel = SanitizeSegment(sourceName);
		return $"{SOURCE_PREFIX}_{sanitizedLabel}_{index:000}";
	}

	/// <summary>
	/// Sanitizes link-label text using filesystem-safe ASCII-only behavior.
	/// </summary>
	/// <param name="value">Raw label text.</param>
	/// <returns>Sanitized label value.</returns>
	private static string SanitizeSegment(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return EMPTY_LABEL_FALLBACK;
		}

		char[]? rentedBuffer = null;
		Span<char> buffer = value.Length <= STACKALLOC_SANITIZE_THRESHOLD
			? stackalloc char[value.Length]
			: new Span<char>(
				rentedBuffer = ArrayPool<char>.Shared.Rent(value.Length),
				0,
				value.Length);

		try
		{
			int outputIndex = 0;
			bool previousWasUnderscore = false;

			for (int index = 0; index < value.Length; index++)
			{
				char character = value[index];
				if (IsAsciiAlphaNumeric(character))
				{
					buffer[outputIndex++] = character;
					previousWasUnderscore = false;
					continue;
				}

				if (previousWasUnderscore)
				{
					continue;
				}

				buffer[outputIndex++] = '_';
				previousWasUnderscore = true;
			}

			string candidate = new(buffer[..outputIndex]);
			string trimmedCandidate = candidate.Trim('_');
			return trimmedCandidate.Length == 0
				? EMPTY_LABEL_FALLBACK
				: trimmedCandidate;
		}
		finally
		{
			if (rentedBuffer is not null)
			{
				ArrayPool<char>.Shared.Return(rentedBuffer, clearArray: true);
			}
		}
	}

	/// <summary>
	/// Determines whether a character is an ASCII letter or digit.
	/// </summary>
	/// <param name="character">Character to inspect.</param>
	/// <returns><see langword="true"/> when ASCII alphanumeric; otherwise <see langword="false"/>.</returns>
	private static bool IsAsciiAlphaNumeric(char character)
	{
		return (character >= 'A' && character <= 'Z')
			|| (character >= 'a' && character <= 'z')
			|| (character >= '0' && character <= '9');
	}
}
