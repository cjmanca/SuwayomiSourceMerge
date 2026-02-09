namespace SuwayomiSourceMerge.Configuration.Bootstrap;

internal sealed class LegacyMigrationResult<TDocument>
    where TDocument : class
{
    public required TDocument Document
    {
        get;
        init;
    }

    public required IReadOnlyList<ConfigurationBootstrapWarning> Warnings
    {
        get;
        init;
    }
}
