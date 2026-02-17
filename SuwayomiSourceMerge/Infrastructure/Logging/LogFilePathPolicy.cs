namespace SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Centralized rules for validating and resolving the configured log file path safely.
/// </summary>
/// <remarks>
/// This policy prevents path traversal and rooted-path overrides by requiring
/// <c>logging.file_name</c> to be a single file name segment and by enforcing that the resolved
/// path remains under <c>paths.log_root_path</c>. It also applies cross-platform strict invalid
/// character checks and rejects reserved Windows device names on all platforms for deterministic behavior.
/// </remarks>
internal static class LogFilePathPolicy
{
	/// <summary>
	/// Cross-platform strict invalid file-name characters.
	/// </summary>
	/// <remarks>
	/// These include Windows-invalid file-name characters and are intentionally applied on all platforms
	/// for deterministic validation behavior.
	/// </remarks>
	private static readonly HashSet<char> _invalidFileNameCharacters =
	[
		'<',
		'>',
		':',
		'"',
		'/',
		'\\',
		'|',
		'?',
		'*'
	];

	/// <summary>
	/// Reserved Windows device names that cannot be used as file names.
	/// </summary>
	private static readonly HashSet<string> _reservedWindowsDeviceNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"CON",
		"PRN",
		"AUX",
		"NUL",
		"COM1",
		"COM2",
		"COM3",
		"COM4",
		"COM5",
		"COM6",
		"COM7",
		"COM8",
		"COM9",
		"LPT1",
		"LPT2",
		"LPT3",
		"LPT4",
		"LPT5",
		"LPT6",
		"LPT7",
		"LPT8",
		"LPT9"
	};

	/// <summary>
	/// Message used when the configured log file name violates file-name safety rules.
	/// </summary>
	public const string InvalidFileNameMessage =
		"Settings logging.file_name must be a single file name without directory segments, invalid file-name characters, or trailing dots/spaces; reserved Windows device names are always rejected.";

	/// <summary>
	/// Validates a configured log file name and returns a normalized value when valid.
	/// </summary>
	/// <param name="value">Raw file name value from configuration.</param>
	/// <param name="normalizedFileName">Trimmed file name when valid; empty string when invalid.</param>
	/// <returns>
	/// <see langword="true"/> when <paramref name="value"/> is a single, safe file name segment;
	/// otherwise <see langword="false"/>.
	/// </returns>
	public static bool TryValidateFileName(string? value, out string normalizedFileName)
	{
		normalizedFileName = string.Empty;
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		string trimmed = value.Trim();
		if (!string.Equals(value, trimmed, StringComparison.Ordinal))
		{
			return false;
		}

		if (Path.IsPathRooted(trimmed))
		{
			return false;
		}

		if (trimmed is "." or "..")
		{
			return false;
		}

		if (trimmed.EndsWith(' ') || trimmed.EndsWith('.'))
		{
			return false;
		}

		if (trimmed.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
			|| trimmed.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
			|| trimmed.Contains('/', StringComparison.Ordinal)
			|| trimmed.Contains('\\', StringComparison.Ordinal))
		{
			return false;
		}

		if (HasInvalidCharacters(trimmed))
		{
			return false;
		}

		if (IsReservedWindowsDeviceName(trimmed))
		{
			return false;
		}

		if (!string.Equals(Path.GetFileName(trimmed), trimmed, StringComparison.Ordinal))
		{
			return false;
		}

		normalizedFileName = trimmed;
		return true;
	}

	/// <summary>
	/// Resolves the final log file path under a configured root path, throwing on unsafe input.
	/// </summary>
	/// <param name="rootPath">Absolute log root directory configured by settings.</param>
	/// <param name="fileName">Configured log file name.</param>
	/// <returns>Canonical full path for the active log file.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="rootPath"/> is null, empty, or whitespace.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="fileName"/> is unsafe or resolves outside <paramref name="rootPath"/>.
	/// </exception>
	public static string ResolvePathUnderRootOrThrow(string rootPath, string? fileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

		if (!TryValidateFileName(fileName, out string normalizedFileName))
		{
			throw new InvalidOperationException(InvalidFileNameMessage);
		}

		string fullRootPath = Path.GetFullPath(rootPath);
		string fullCandidatePath = Path.GetFullPath(Path.Combine(fullRootPath, normalizedFileName));
		if (!IsUnderRoot(fullRootPath, fullCandidatePath))
		{
			throw new InvalidOperationException(
				"Settings logging.file_name resolves outside paths.log_root_path.");
		}

		return fullCandidatePath;
	}

	/// <summary>
	/// Determines whether a file name contains cross-platform strict invalid characters.
	/// </summary>
	/// <param name="value">Candidate file name.</param>
	/// <returns>
	/// <see langword="true"/> when the value contains a control character (U+0000 to U+001F) or
	/// a configured invalid file-name character.
	/// </returns>
	private static bool HasInvalidCharacters(string value)
	{
		foreach (char character in value)
		{
			if (character <= '\u001F')
			{
				return true;
			}

			if (_invalidFileNameCharacters.Contains(character))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether a file name is a reserved Windows device name.
	/// </summary>
	/// <param name="value">Candidate file name.</param>
	/// <returns>
	/// <see langword="true"/> when the base file name (before extension) matches a reserved device name.
	/// </returns>
	private static bool IsReservedWindowsDeviceName(string value)
	{
		string baseName = Path.GetFileNameWithoutExtension(value);
		return _reservedWindowsDeviceNames.Contains(baseName);
	}

	/// <summary>
	/// Determines whether a candidate full path is located under the specified root path.
	/// </summary>
	/// <param name="rootPath">Canonical root path.</param>
	/// <param name="candidatePath">Canonical candidate path to check.</param>
	/// <returns><see langword="true"/> when <paramref name="candidatePath"/> is under <paramref name="rootPath"/>.</returns>
	private static bool IsUnderRoot(string rootPath, string candidatePath)
	{
		StringComparison comparison = OperatingSystem.IsWindows()
			? StringComparison.OrdinalIgnoreCase
			: StringComparison.Ordinal;

		string trimmedRootPath = Path.TrimEndingDirectorySeparator(rootPath);
		string directorySeparator = Path.DirectorySeparatorChar.ToString();
		string rootWithSeparator = trimmedRootPath.EndsWith(directorySeparator, StringComparison.Ordinal)
			? trimmedRootPath
			: string.Concat(trimmedRootPath, Path.DirectorySeparatorChar);

		return candidatePath.StartsWith(rootWithSeparator, comparison);
	}
}
