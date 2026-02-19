using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Serializes configuration document objects into canonical YAML files on disk.
/// </summary>
/// <remarks>
/// The writer enforces underscored naming, disables YAML aliases, and normalizes line endings to
/// Unix-style newlines so generated config files are deterministic across platforms.
/// Null-valued properties are omitted so deprecated/optional fields are not reintroduced as explicit
/// <c>null</c> entries during self-heal rewrites.
/// </remarks>
internal sealed class YamlDocumentWriter
{
	/// <summary>
	/// Shared serializer configured for canonical configuration output.
	/// </summary>
	private readonly ISerializer _serializer;

	/// <summary>
	/// Initializes a new <see cref="YamlDocumentWriter"/> with canonical serialization settings.
	/// </summary>
	public YamlDocumentWriter()
	{
		_serializer = new SerializerBuilder()
			.DisableAliases()
			.ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
			.WithNamingConvention(UnderscoredNamingConvention.Instance)
			.Build();
	}

	/// <summary>
	/// Writes a document to YAML at the supplied output path.
	/// </summary>
	/// <typeparam name="TDocument">Document type to serialize.</typeparam>
	/// <param name="outputPath">Absolute or relative output path for the YAML file.</param>
	/// <param name="document">Document instance to serialize.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="outputPath"/> is empty or whitespace.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <see langword="null"/>.</exception>
	/// <exception cref="IOException">Thrown when the destination cannot be created or written.</exception>
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
