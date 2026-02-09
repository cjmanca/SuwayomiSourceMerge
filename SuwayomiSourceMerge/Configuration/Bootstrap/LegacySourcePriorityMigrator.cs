using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

internal sealed class LegacySourcePriorityMigrator
{
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
