namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Centralizes deterministic path-segment escaping and strict containment validation for mount planner paths.
/// </summary>
internal static class PathSafetyPolicy
{
	/// <summary>
	/// Invalid filename characters rejected by deterministic link-name validation.
	/// </summary>
	private static readonly char[] INVALID_LINK_NAME_CHARACTERS = ['<', '>', ':', '"', '|', '?', '*'];

	/// <summary>
	/// Reserved current-directory segment token.
	/// </summary>
	private const string CURRENT_DIRECTORY_SEGMENT = ".";

	/// <summary>
	/// Reserved parent-directory segment token.
	/// </summary>
	private const string PARENT_DIRECTORY_SEGMENT = "..";

	/// <summary>
	/// Escaped replacement for the current-directory segment.
	/// </summary>
	private const string ESCAPED_CURRENT_DIRECTORY_SEGMENT = "_ssm_dot_";

	/// <summary>
	/// Escaped replacement for the parent-directory segment.
	/// </summary>
	private const string ESCAPED_PARENT_DIRECTORY_SEGMENT = "_ssm_dotdot_";

	/// <summary>
	/// Path comparer used for path-equality and ordering checks.
	/// </summary>
	private static readonly StringComparer PATH_COMPARER = OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	/// <summary>
	/// Path comparison used for containment checks.
	/// </summary>
	private static readonly StringComparison PATH_COMPARISON = OperatingSystem.IsWindows()
		? StringComparison.OrdinalIgnoreCase
		: StringComparison.Ordinal;

	/// <summary>
	/// Gets the OS-aware path comparer used by mount planning for path ordering and de-duplication.
	/// </summary>
	/// <returns>OS-aware path comparer.</returns>
	public static StringComparer GetPathComparer()
	{
		return PATH_COMPARER;
	}

	/// <summary>
	/// Determines whether two fully-qualified absolute paths refer to the same path using OS-aware semantics.
	/// </summary>
	/// <param name="firstPath">First fully-qualified absolute path.</param>
	/// <param name="secondPath">Second fully-qualified absolute path.</param>
	/// <returns><see langword="true"/> when both paths are equal under OS-aware comparison; otherwise <see langword="false"/>.</returns>
	/// <exception cref="ArgumentException">Thrown when either path is null, empty, or not fully-qualified absolute.</exception>
	public static bool ArePathsEqual(string firstPath, string secondPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(firstPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(secondPath);

		string normalizedFirstPath = NormalizeFullyQualifiedPath(firstPath, nameof(firstPath));
		string normalizedSecondPath = NormalizeFullyQualifiedPath(secondPath, nameof(secondPath));
		return string.Equals(normalizedFirstPath, normalizedSecondPath, PATH_COMPARISON);
	}

	/// <summary>
	/// Determines whether the provided value contains a directory separator.
	/// </summary>
	/// <param name="value">Value to inspect.</param>
	/// <returns><see langword="true"/> when a directory separator exists; otherwise <see langword="false"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
	public static bool ContainsDirectorySeparator(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		return value.Contains('/', StringComparison.Ordinal)
			|| value.Contains('\\', StringComparison.Ordinal);
	}

	/// <summary>
	/// Validates one link-name segment against deterministic cross-platform safety rules.
	/// </summary>
	/// <param name="value">Link-name segment value to validate.</param>
	/// <param name="paramName">Parameter name to associate with argument exceptions.</param>
	/// <exception cref="ArgumentException">Thrown when value violates link-name safety constraints.</exception>
	public static void ValidateLinkNameSegment(string value, string paramName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		ArgumentException.ThrowIfNullOrWhiteSpace(paramName);

		if (ContainsDirectorySeparator(value))
		{
			throw new ArgumentException(
				"Link name must not contain directory separators.",
				paramName);
		}

		if (IsDangerousDotSegment(value))
		{
			throw new ArgumentException(
				"Link name must not be a reserved dot-segment value.",
				paramName);
		}

		if (ContainsInvalidLinkNameCharacters(value))
		{
			throw new ArgumentException(
				"Link name contains invalid characters.",
				paramName);
		}

		if (HasTrailingDotOrSpace(value))
		{
			throw new ArgumentException(
				"Link name must not end with a dot or space.",
				paramName);
		}
	}

	/// <summary>
	/// Escapes reserved dot-segment values while leaving all other values unchanged.
	/// </summary>
	/// <param name="segment">Path segment to escape when reserved.</param>
	/// <returns>Escaped reserved segment, or the original value when not reserved.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="segment"/> is null, empty, or whitespace.</exception>
	public static string EscapeReservedSegment(string segment)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(segment);

		return segment switch
		{
			CURRENT_DIRECTORY_SEGMENT => ESCAPED_CURRENT_DIRECTORY_SEGMENT,
			PARENT_DIRECTORY_SEGMENT => ESCAPED_PARENT_DIRECTORY_SEGMENT,
			_ => segment
		};
	}

	/// <summary>
	/// Resolves and validates that <paramref name="candidatePath"/> is a strict child path under <paramref name="rootPath"/>.
	/// </summary>
	/// <param name="rootPath">Root path that must contain the candidate path.</param>
	/// <param name="candidatePath">Candidate path to resolve and validate.</param>
	/// <param name="paramName">Parameter name to associate with candidate-path validation and containment failures.</param>
	/// <returns>Normalized absolute candidate path.</returns>
	/// <exception cref="ArgumentException">Thrown when arguments are malformed or containment fails.</exception>
	public static string EnsureStrictChildPath(string rootPath, string candidatePath, string paramName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(paramName);

		string normalizedRootPath = NormalizeFullyQualifiedPath(rootPath, nameof(rootPath));
		string normalizedCandidatePath = NormalizeFullyQualifiedPath(candidatePath, paramName);

		if (string.Equals(normalizedRootPath, normalizedCandidatePath, PATH_COMPARISON))
		{
			throw new ArgumentException(
				"Path must be a strict child of the root path and must not equal the root path.",
				paramName);
		}

		string rootPrefix = AppendTrailingDirectorySeparator(normalizedRootPath);
		if (!normalizedCandidatePath.StartsWith(rootPrefix, PATH_COMPARISON))
		{
			throw new ArgumentException(
				"Path must remain under the provided root path.",
				paramName);
		}

		return normalizedCandidatePath;
	}

	/// <summary>
	/// Normalizes and validates one fully-qualified absolute path value.
	/// </summary>
	/// <param name="value">Path value to normalize.</param>
	/// <param name="paramName">Parameter name to associate with argument exceptions.</param>
	/// <returns>Normalized absolute path.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is not fully-qualified absolute.</exception>
	public static string NormalizeFullyQualifiedPath(string value, string paramName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		ArgumentException.ThrowIfNullOrWhiteSpace(paramName);

		string trimmedPath = value.Trim();
		if (!Path.IsPathFullyQualified(trimmedPath))
		{
			throw new ArgumentException(
				"Path must be a fully-qualified absolute path.",
				paramName);
		}

		return Path.GetFullPath(trimmedPath);
	}

	/// <summary>
	/// Ensures one trailing directory separator exists.
	/// </summary>
	/// <param name="path">Path to normalize.</param>
	/// <returns>Path with one trailing directory separator.</returns>
	private static string AppendTrailingDirectorySeparator(string path)
	{
		return Path.EndsInDirectorySeparator(path)
			? path
			: path + Path.DirectorySeparatorChar;
	}

	/// <summary>
	/// Determines whether a link-name value is one of the reserved dot-segment tokens.
	/// </summary>
	/// <param name="value">Value to inspect.</param>
	/// <returns><see langword="true"/> when reserved; otherwise <see langword="false"/>.</returns>
	private static bool IsDangerousDotSegment(string value)
	{
		return string.Equals(value, CURRENT_DIRECTORY_SEGMENT, StringComparison.Ordinal)
			|| string.Equals(value, PARENT_DIRECTORY_SEGMENT, StringComparison.Ordinal);
	}

	/// <summary>
	/// Determines whether a link-name value contains one or more invalid filename characters.
	/// </summary>
	/// <param name="value">Value to inspect.</param>
	/// <returns><see langword="true"/> when invalid filename characters are present; otherwise <see langword="false"/>.</returns>
	private static bool ContainsInvalidLinkNameCharacters(string value)
	{
		for (int index = 0; index < value.Length; index++)
		{
			char character = value[index];
			if (char.IsControl(character))
			{
				return true;
			}

			if (IsInvalidLinkNameCharacter(character))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether one character is in the invalid filename-character denylist.
	/// </summary>
	/// <param name="character">Character to inspect.</param>
	/// <returns><see langword="true"/> when invalid; otherwise <see langword="false"/>.</returns>
	private static bool IsInvalidLinkNameCharacter(char character)
	{
		for (int index = 0; index < INVALID_LINK_NAME_CHARACTERS.Length; index++)
		{
			if (INVALID_LINK_NAME_CHARACTERS[index] == character)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether a link-name value ends with a trailing dot or space.
	/// </summary>
	/// <param name="value">Value to inspect.</param>
	/// <returns><see langword="true"/> when a trailing dot or space exists; otherwise <see langword="false"/>.</returns>
	private static bool HasTrailingDotOrSpace(string value)
	{
		if (value.Length == 0)
		{
			return false;
		}

		char trailingCharacter = value[^1];
		return trailingCharacter == '.'
			|| trailingCharacter == ' ';
	}
}
