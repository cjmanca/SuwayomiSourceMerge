using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Converts legacy <c>source_priority.txt</c> content into the canonical YAML document model.
/// </summary>
/// <remarks>
/// Blank lines and comment lines are ignored to match legacy shell behavior while preserving explicit
/// source ordering from top to bottom.
/// </remarks>
internal sealed class LegacySourcePriorityMigrator
{
	/// <summary>
	/// Reads and converts a legacy source-priority file into a typed document.
	/// </summary>
	/// <param name="legacyFilePath">Absolute path to <c>source_priority.txt</c>.</param>
	/// <returns>
	/// A migration result containing the converted <see cref="SourcePriorityDocument"/> and no warnings.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="legacyFilePath"/> is empty or whitespace.</exception>
	/// <exception cref="IOException">Thrown when the file cannot be read.</exception>
	public LegacyMigrationResult<SourcePriorityDocument> Migrate(string legacyFilePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(legacyFilePath);

		string[] lines = File.ReadAllLines(legacyFilePath);

		List<string> sources = [];

		foreach (string line in lines)
		{
			string trimmedLine = line.Trim();
			if (string.IsNullOrWhiteSpace(trimmedLine))
			{
				continue;
			}

			if (trimmedLine.StartsWith('#'))
			{
				continue;
			}

			sources.Add(trimmedLine);
		}

		return new LegacyMigrationResult<SourcePriorityDocument>
		{
			Document = new SourcePriorityDocument
			{
				Sources = sources
			},
			Warnings = []
		};
	}
}
