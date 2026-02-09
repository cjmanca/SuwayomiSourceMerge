namespace SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Represents successful bootstrap output, including validated documents and diagnostics.
/// </summary>
public sealed class ConfigurationBootstrapResult
{
    /// <summary>
    /// Gets the validated configuration documents.
    /// </summary>
    public required ConfigurationDocumentSet Documents
    {
        get;
        init;
    }

    /// <summary>
    /// Gets bootstrap file state metadata in deterministic file order.
    /// </summary>
    public required IReadOnlyList<ConfigurationBootstrapFileState> Files
    {
        get;
        init;
    }

    /// <summary>
    /// Gets non-fatal warnings produced during bootstrap operations.
    /// </summary>
    public required IReadOnlyList<ConfigurationBootstrapWarning> Warnings
    {
        get;
        init;
    }
}
