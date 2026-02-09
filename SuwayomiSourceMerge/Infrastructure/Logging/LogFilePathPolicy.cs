namespace SuwayomiSourceMerge.Infrastructure.Logging;

internal static class LogFilePathPolicy
{
    public const string InvalidFileNameMessage =
        "Settings logging.file_name must be a single file name without directory segments.";

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

        if (trimmed.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || trimmed.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || trimmed.Contains('/', StringComparison.Ordinal)
            || trimmed.Contains('\\', StringComparison.Ordinal))
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
