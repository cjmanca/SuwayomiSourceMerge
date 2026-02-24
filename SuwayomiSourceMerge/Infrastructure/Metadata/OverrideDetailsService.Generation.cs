using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Generation entry helpers for <see cref="OverrideDetailsService"/>.
/// </summary>
internal sealed partial class OverrideDetailsService
{
	/// <summary>
	/// Attempts to parse one ComicInfo.xml file and write shell-parity details.json content.
	/// </summary>
	/// <param name="comicInfoXmlPath">ComicInfo.xml path to parse.</param>
	/// <param name="request">Ensure request.</param>
	/// <param name="detailsJsonPath">details.json output path.</param>
	/// <param name="destinationAlreadyExists">Whether destination details.json already exists after a handled race condition.</param>
	/// <returns><see langword="true"/> when parsing and writing succeed; otherwise <see langword="false"/>.</returns>
	private bool TryGenerateFromComicInfo(
		string comicInfoXmlPath,
		OverrideDetailsRequest request,
		string detailsJsonPath,
		out bool destinationAlreadyExists)
	{
		if (!_comicInfoMetadataParser.TryParse(comicInfoXmlPath, out ComicInfoMetadata? metadata) || metadata is null)
		{
			destinationAlreadyExists = false;
			return false;
		}

		DetailsJsonDocumentModel output = BuildComicInfoDetailsJsonModel(
			request.DisplayTitle,
			request.DetailsDescriptionMode,
			metadata);

		return TryWriteDetailsJsonNonOverwriting(detailsJsonPath, output, out destinationAlreadyExists);
	}

	/// <summary>
	/// Attempts API-first details generation using one matched Comick payload with optional ComicInfo field fallback.
	/// </summary>
	/// <param name="request">Ensure request.</param>
	/// <param name="detailsJsonPath">details.json output path.</param>
	/// <param name="fallbackComicInfoPath">ComicInfo.xml path only when one or more fields used fallback values.</param>
	/// <param name="destinationAlreadyExists">Whether destination details.json already exists after a handled race condition.</param>
	/// <returns><see langword="true"/> when generation and write succeed; otherwise <see langword="false"/>.</returns>
	private bool TryGenerateFromComick(
		OverrideDetailsRequest request,
		string detailsJsonPath,
		out string? fallbackComicInfoPath,
		out bool destinationAlreadyExists)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentException.ThrowIfNullOrWhiteSpace(detailsJsonPath);

		ComickComicResponse comickComic = request.MatchedComickComic
			?? throw new ArgumentException(
				"Matched Comick payload is required for API-first generation.",
				nameof(request));

		DetailsJsonDocumentModel output = BuildComickPreferredDetailsJsonModel(
			request.DisplayTitle,
			request.DetailsDescriptionMode,
			comickComic,
			request.OrderedSourceDirectoryPaths,
			out fallbackComicInfoPath);

		return TryWriteDetailsJsonNonOverwriting(detailsJsonPath, output, out destinationAlreadyExists);
	}
}
