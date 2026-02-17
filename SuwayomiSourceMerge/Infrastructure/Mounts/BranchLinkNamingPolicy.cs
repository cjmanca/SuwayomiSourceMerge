using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Produces deterministic filesystem-safe branch-link names for override and source branches.
/// </summary>
internal sealed class BranchLinkNamingPolicy
{
	/// <summary>
	/// Link name used for the primary read-write override branch.
	/// </summary>
	private const string PrimaryOverrideLinkName = "00_override_primary";

	/// <summary>
	/// Prefix used for additional read-write override branches.
	/// </summary>
	private const string ExtraOverridePrefix = "01_override";

	/// <summary>
	/// Prefix used for read-only source branches.
	/// </summary>
	private const string SourcePrefix = "10_source";

	/// <summary>
	/// Placeholder label used when sanitization produces an empty value.
	/// </summary>
	private const string EmptyLabelFallback = "x";

	/// <summary>
	/// Maximum label length processed on the stack before falling back to pooled buffers.
	/// </summary>
	private const int StackallocSanitizeThreshold = 256;

	/// <summary>
	/// Maximum allowed filesystem path component length for generated link names.
	/// </summary>
	private const int MaxLinkNameComponentLength = 255;

	/// <summary>
	/// Delimiter length used between link-name parts.
	/// </summary>
	private const int LinkLabelDelimiterLength = 1;

	/// <summary>
	/// Length of the zero-padded branch index token.
	/// </summary>
	private const int ZeroPaddedIndexLength = 3;

	/// <summary>
	/// Length of the <c>_000</c>-style suffix in generated link names.
	/// </summary>
	private const int IndexSuffixLength = LinkLabelDelimiterLength + ZeroPaddedIndexLength;

	/// <summary>
	/// Length of the hash suffix appended when long labels are truncated.
	/// </summary>
	private const int HashSuffixHexLength = 12;

	/// <summary>
	/// Maximum sanitized label length for additional-override link names.
	/// </summary>
	private static readonly int _additionalOverrideLabelMaxLength = MaxLinkNameComponentLength
		- ExtraOverridePrefix.Length
		- LinkLabelDelimiterLength
		- IndexSuffixLength;

	/// <summary>
	/// Maximum sanitized label length for source link names.
	/// </summary>
	private static readonly int _sourceLabelMaxLength = MaxLinkNameComponentLength
		- SourcePrefix.Length
		- LinkLabelDelimiterLength
		- IndexSuffixLength;

	/// <summary>
	/// Builds the deterministic primary override link name.
	/// </summary>
	/// <returns>Primary override link name.</returns>
	public string BuildPrimaryOverrideLinkName()
	{
		return PrimaryOverrideLinkName;
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
		string sanitizedLabel = SanitizeSegment(volumeLabel, _additionalOverrideLabelMaxLength);
		return $"{ExtraOverridePrefix}_{sanitizedLabel}_{index:000}";
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

		string sanitizedLabel = SanitizeSegment(sourceName, _sourceLabelMaxLength);
		return $"{SourcePrefix}_{sanitizedLabel}_{index:000}";
	}

	/// <summary>
	/// Sanitizes link-label text using filesystem-safe ASCII-only behavior.
	/// </summary>
	/// <param name="value">Raw label text.</param>
	/// <param name="maxLabelLength">Maximum allowed length of the sanitized label segment.</param>
	/// <returns>Sanitized label value.</returns>
	private static string SanitizeSegment(string value, int maxLabelLength)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(maxLabelLength, 1);

		if (string.IsNullOrWhiteSpace(value))
		{
			return EmptyLabelFallback;
		}

		char[]? rentedBuffer = null;
		Span<char> buffer = value.Length <= StackallocSanitizeThreshold
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
			string normalizedLabel = trimmedCandidate.Length == 0
				? EmptyLabelFallback
				: trimmedCandidate;
			return EnsureLabelLength(normalizedLabel, maxLabelLength);
		}
		finally
		{
			if (rentedBuffer is not null)
			{
				ArrayPool<char>.Shared.Return(rentedBuffer, clearArray: false);
			}
		}
	}

	/// <summary>
	/// Ensures one sanitized label fits within the requested maximum length.
	/// </summary>
	/// <param name="label">Sanitized label text.</param>
	/// <param name="maxLabelLength">Maximum allowed label length.</param>
	/// <returns>Label text constrained to <paramref name="maxLabelLength"/> characters.</returns>
	private static string EnsureLabelLength(string label, int maxLabelLength)
	{
		if (label.Length <= maxLabelLength)
		{
			return label;
		}

		string hashSuffix = ComputeHashPrefix(label, HashSuffixHexLength);
		if (maxLabelLength <= HashSuffixHexLength)
		{
			return hashSuffix[..maxLabelLength];
		}

		int prefixBudget = maxLabelLength - LinkLabelDelimiterLength - HashSuffixHexLength;
		string prefix = label[..prefixBudget].TrimEnd('_');
		if (prefix.Length == 0)
		{
			prefix = EmptyLabelFallback;
		}

		string candidate = $"{prefix}_{hashSuffix}";
		if (candidate.Length <= maxLabelLength)
		{
			return candidate;
		}

		int overflowLength = candidate.Length - maxLabelLength;
		if (overflowLength >= prefix.Length)
		{
			return hashSuffix[..maxLabelLength];
		}

		string truncatedPrefix = prefix[..(prefix.Length - overflowLength)];
		return $"{truncatedPrefix}_{hashSuffix}";
	}

	/// <summary>
	/// Computes a lowercase SHA-256 hash prefix for deterministic truncation suffixes.
	/// </summary>
	/// <param name="value">Input text to hash.</param>
	/// <param name="length">Requested hash-prefix length.</param>
	/// <returns>Lowercase hexadecimal hash prefix.</returns>
	private static string ComputeHashPrefix(string value, int length)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);

		byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		string hashText = Convert.ToHexString(hashBytes).ToLowerInvariant();
		return hashText[..length];
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
