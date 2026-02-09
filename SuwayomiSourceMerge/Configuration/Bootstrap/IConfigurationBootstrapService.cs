using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Provides first-run configuration bootstrap and legacy migration behavior.
/// </summary>
public interface IConfigurationBootstrapService
{
    /// <summary>
    /// Ensures required configuration files exist, migrates legacy files when needed, and validates all documents.
    /// </summary>
    /// <param name="configRootPath">Root directory that contains all configuration files.</param>
    /// <returns>The validated configuration documents and bootstrap diagnostics.</returns>
    /// <exception cref="ConfigurationBootstrapException">Thrown when validation fails after bootstrap operations complete.</exception>
    ConfigurationBootstrapResult Bootstrap(string configRootPath);
}
