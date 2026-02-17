using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Converts legacy <c>manga_equivalents.txt</c> content into the canonical YAML document model.
/// </summary>
/// <remarks>
/// The migrator is intentionally tolerant: malformed lines are skipped and emitted as warnings so
/// bootstrap can proceed while still surfacing actionable diagnostics to the caller.
/// </remarks>
internal sealed class LegacyMangaEquivalentsMigrator
{
	/// <summary>
	/// Warning code emitted when a legacy line does not contain a canonical title.
	/// </summary>
	private const string MissingCanonicalWarningCode = "CFG-MIG-001";

	/// <summary>
	/// Reads and converts a legacy manga equivalence file into a typed document.
	/// </summary>
	/// <param name="legacyFilePath">Absolute path to <c>manga_equivalents.txt</c>.</param>
	/// <returns>
	/// A migration result containing the converted <see cref="MangaEquivalentsDocument"/> and any warnings.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="legacyFilePath"/> is empty or whitespace.</exception>
	/// <exception cref="IOException">Thrown when the file cannot be read.</exception>
	public LegacyMigrationResult<MangaEquivalentsDocument> Migrate(string legacyFilePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(legacyFilePath);

		string fileName = Path.GetFileName(legacyFilePath);
		string[] lines = File.ReadAllLines(legacyFilePath);

		List<MangaEquivalentGroup> groups = [];
		List<ConfigurationBootstrapWarning> warnings = [];

		for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
		{
			string rawLine = lines[lineIndex];
			string trimmedLine = rawLine.Trim();

			if (string.IsNullOrWhiteSpace(trimmedLine))
			{
				continue;
			}

			if (trimmedLine.StartsWith('#'))
			{
				continue;
			}

			string[] parts = rawLine.Split('|');
			string canonical = parts[0].Trim();

			if (string.IsNullOrWhiteSpace(canonical))
			{
				warnings.Add(
					new ConfigurationBootstrapWarning(
						MissingCanonicalWarningCode,
						fileName,
						lineIndex + 1,
						"Skipped legacy line because canonical value is empty."));
				continue;
			}

			List<string> aliases = [];
			for (int partIndex = 1; partIndex < parts.Length; partIndex++)
			{
				string alias = parts[partIndex].Trim();
				if (string.IsNullOrWhiteSpace(alias))
				{
					continue;
				}

				if (alias.StartsWith('#'))
				{
					continue;
				}

				aliases.Add(alias);
			}

			groups.Add(
				new MangaEquivalentGroup
				{
					Canonical = canonical,
					Aliases = aliases
				});
		}

		return new LegacyMigrationResult<MangaEquivalentsDocument>
		{
			Document = new MangaEquivalentsDocument
			{
				Groups = groups
			},
			Warnings = warnings
		};
	}
}
