namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Parses ComicInfo.xml files into metadata fields used for details.json generation.
/// </summary>
internal interface IComicInfoMetadataParser
{
	/// <summary>
	/// Attempts to parse one ComicInfo.xml file into a metadata model.
	/// </summary>
	/// <param name="comicInfoXmlPath">Absolute path to ComicInfo.xml.</param>
	/// <param name="metadata">Parsed metadata when parsing succeeds.</param>
	/// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="comicInfoXmlPath"/> is empty or whitespace.</exception>
	bool TryParse(string comicInfoXmlPath, out ComicInfoMetadata? metadata);
}
