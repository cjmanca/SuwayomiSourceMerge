using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

internal sealed class LegacyMangaEquivalentsMigrator
{
    private const string MISSING_CANONICAL_WARNING_CODE = "CFG-MIG-001";

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
                        MISSING_CANONICAL_WARNING_CODE,
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
