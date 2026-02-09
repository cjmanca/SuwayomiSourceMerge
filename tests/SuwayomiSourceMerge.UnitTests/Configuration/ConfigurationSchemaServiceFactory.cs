namespace SuwayomiSourceMerge.UnitTests.Configuration;

using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Configuration.Loading;

internal static class ConfigurationSchemaServiceFactory
{
    public static ConfigurationBootstrapService CreateBootstrapService()
    {
        return new ConfigurationBootstrapService(CreateSchemaService());
    }

    public static ConfigurationSchemaService CreateSchemaService()
    {
        return new ConfigurationSchemaService(
            new ConfigurationValidationPipeline(
                new YamlDocumentParser()));
    }

    public static ConfigurationSchemaService Create()
    {
        return CreateSchemaService();
    }
}
