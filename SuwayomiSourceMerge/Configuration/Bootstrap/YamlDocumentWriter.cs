using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

internal sealed class YamlDocumentWriter
{
    private readonly ISerializer _serializer;

    public YamlDocumentWriter()
    {
        _serializer = new SerializerBuilder()
            .DisableAliases()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    public void Write<TDocument>(string outputPath, TDocument document)
        where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(document);

        string yaml = _serializer.Serialize(document)
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        if (!yaml.EndsWith('\n'))
        {
            yaml += '\n';
        }

        string? directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllText(outputPath, yaml);
    }
}
