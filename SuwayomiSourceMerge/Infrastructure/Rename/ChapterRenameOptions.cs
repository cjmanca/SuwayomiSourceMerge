using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Carries validated chapter rename runtime settings.
/// </summary>
internal sealed class ChapterRenameOptions
{
	/// <summary>
	/// Normalized excluded source-name keys.
	/// </summary>
	private readonly HashSet<string> _excludedSourceKeys;

	/// <summary>
	/// Initializes a new instance of the <see cref="ChapterRenameOptions"/> class.
	/// </summary>
	/// <param name="sourcesRootPath">Root path containing source directories.</param>
	/// <param name="renameDelaySeconds">Delay in seconds before processing one queued chapter path.</param>
	/// <param name="renameQuietSeconds">Required quiet window in seconds before renaming.</param>
	/// <param name="renamePollSeconds">Queue polling interval in seconds.</param>
	/// <param name="renameRescanSeconds">Rescan/grace interval in seconds for missing paths.</param>
	/// <param name="excludedSources">Excluded source names.</param>
	public ChapterRenameOptions(
		string sourcesRootPath,
		int renameDelaySeconds,
		int renameQuietSeconds,
		int renamePollSeconds,
		int renameRescanSeconds,
		IReadOnlyList<string> excludedSources)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcesRootPath);
		ArgumentNullException.ThrowIfNull(excludedSources);

		if (renameDelaySeconds < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(renameDelaySeconds), "Rename delay seconds must be >= 0.");
		}

		if (renameQuietSeconds < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(renameQuietSeconds), "Rename quiet seconds must be >= 0.");
		}

		if (renamePollSeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(renamePollSeconds), "Rename poll seconds must be > 0.");
		}

		if (renameRescanSeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(renameRescanSeconds), "Rename rescan seconds must be > 0.");
		}

		SourcesRootPath = Path.GetFullPath(sourcesRootPath);
		RenameDelaySeconds = renameDelaySeconds;
		RenameQuietSeconds = renameQuietSeconds;
		RenamePollSeconds = renamePollSeconds;
		RenameRescanSeconds = renameRescanSeconds;

		_excludedSourceKeys = new HashSet<string>(StringComparer.Ordinal);
		for (int index = 0; index < excludedSources.Count; index++)
		{
			string? sourceName = excludedSources[index];
			if (string.IsNullOrWhiteSpace(sourceName))
			{
				continue;
			}

			_excludedSourceKeys.Add(NormalizeSourceKey(sourceName));
		}

		ExcludedSources = excludedSources.Where(static sourceName => !string.IsNullOrWhiteSpace(sourceName))
			.Select(static sourceName => sourceName.Trim())
			.ToArray();
	}

	/// <summary>
	/// Gets the normalized source root path.
	/// </summary>
	public string SourcesRootPath
	{
		get;
	}

	/// <summary>
	/// Gets delay in seconds before queued entries become eligible for processing.
	/// </summary>
	public int RenameDelaySeconds
	{
		get;
	}

	/// <summary>
	/// Gets required quiet window in seconds before renaming.
	/// </summary>
	public int RenameQuietSeconds
	{
		get;
	}

	/// <summary>
	/// Gets queue poll interval in seconds.
	/// </summary>
	public int RenamePollSeconds
	{
		get;
	}

	/// <summary>
	/// Gets rescan interval and missing-path grace duration in seconds.
	/// </summary>
	public int RenameRescanSeconds
	{
		get;
	}

	/// <summary>
	/// Gets excluded source names.
	/// </summary>
	public IReadOnlyList<string> ExcludedSources
	{
		get;
	}

	/// <summary>
	/// Builds options from validated settings.
	/// </summary>
	/// <param name="settings">Settings document.</param>
	/// <returns>Resolved chapter rename options.</returns>
	public static ChapterRenameOptions FromSettings(SettingsDocument settings)
	{
		ArgumentNullException.ThrowIfNull(settings);

		if (settings.Paths?.SourcesRootPath is null)
		{
			throw new ArgumentException("Settings paths.sources_root_path is required.", nameof(settings));
		}

		if (settings.Rename is null)
		{
			throw new ArgumentException("Settings rename section is required.", nameof(settings));
		}

		SettingsRenameSection rename = settings.Rename;
		if (!rename.RenameDelaySeconds.HasValue ||
			!rename.RenameQuietSeconds.HasValue ||
			!rename.RenamePollSeconds.HasValue ||
			!rename.RenameRescanSeconds.HasValue)
		{
			throw new ArgumentException("Settings rename section contains missing values.", nameof(settings));
		}

		IReadOnlyList<string> excludedSources = settings.Runtime?.ExcludedSources ?? [];

		return new ChapterRenameOptions(
			settings.Paths.SourcesRootPath,
			rename.RenameDelaySeconds.Value,
			rename.RenameQuietSeconds.Value,
			rename.RenamePollSeconds.Value,
			rename.RenameRescanSeconds.Value,
			excludedSources);
	}

	/// <summary>
	/// Returns whether one source name is excluded.
	/// </summary>
	/// <param name="sourceName">Source name to evaluate.</param>
	/// <returns><see langword="true"/> when the source is excluded.</returns>
	public bool IsExcludedSource(string sourceName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
		return _excludedSourceKeys.Contains(NormalizeSourceKey(sourceName));
	}

	/// <summary>
	/// Normalizes one source name into lookup-key form.
	/// </summary>
	/// <param name="sourceName">Source name.</param>
	/// <returns>Normalized source key.</returns>
	private static string NormalizeSourceKey(string sourceName)
	{
		return sourceName.Trim().ToLowerInvariant();
	}
}

