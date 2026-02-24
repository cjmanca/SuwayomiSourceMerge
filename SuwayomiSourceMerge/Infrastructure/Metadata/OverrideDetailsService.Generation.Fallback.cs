namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// ComicInfo fallback discovery helpers for <see cref="OverrideDetailsService"/>.
/// </summary>
internal sealed partial class OverrideDetailsService
{
	/// <summary>
	/// Attempts to resolve the first parseable ComicInfo metadata candidate for Comick field fallback.
	/// </summary>
	/// <param name="sourceDirectoryPaths">Ordered source directory paths.</param>
	/// <param name="fallbackMetadata">Parsed fallback metadata when found.</param>
	/// <param name="comicInfoXmlPath">Parsed ComicInfo.xml path when found.</param>
	/// <returns><see langword="true"/> when metadata was parsed; otherwise <see langword="false"/>.</returns>
	private bool TryResolveComicInfoFallbackMetadata(
		IReadOnlyList<string> sourceDirectoryPaths,
		out ComicInfoMetadata? fallbackMetadata,
		out string? comicInfoXmlPath)
	{
		ArgumentNullException.ThrowIfNull(sourceDirectoryPaths);

		Dictionary<string, string> fastPathCandidatesBySource = DiscoverFastPathCandidates(sourceDirectoryPaths);
		HashSet<string> attemptedCandidatePaths = new(StringComparer.Ordinal);

		foreach (string sourceDirectoryPath in sourceDirectoryPaths)
		{
			if (!fastPathCandidatesBySource.TryGetValue(sourceDirectoryPath, out string? candidatePath))
			{
				continue;
			}

			if (!attemptedCandidatePaths.Add(candidatePath))
			{
				continue;
			}

			if (_comicInfoMetadataParser.TryParse(candidatePath, out ComicInfoMetadata? parsedMetadata) && parsedMetadata is not null)
			{
				fallbackMetadata = parsedMetadata;
				comicInfoXmlPath = candidatePath;
				return true;
			}
		}

		List<string> slowPathCandidates = DiscoverSlowPathCandidates(sourceDirectoryPaths, fastPathCandidatesBySource);
		for (int index = 0; index < slowPathCandidates.Count; index++)
		{
			string candidatePath = slowPathCandidates[index];
			if (!attemptedCandidatePaths.Add(candidatePath))
			{
				continue;
			}

			if (_comicInfoMetadataParser.TryParse(candidatePath, out ComicInfoMetadata? parsedMetadata) && parsedMetadata is not null)
			{
				fallbackMetadata = parsedMetadata;
				comicInfoXmlPath = candidatePath;
				return true;
			}
		}

		fallbackMetadata = null;
		comicInfoXmlPath = null;
		return false;
	}

	/// <summary>
	/// Resolves fallback metadata once and caches the resolution payload.
	/// </summary>
	private sealed class LazyComicInfoFallbackResolver
	{
		private readonly OverrideDetailsService _service;
		private readonly IReadOnlyList<string> _sourceDirectoryPaths;
		private bool _hasResolution;
		private ComicInfoFallbackResolution _resolution;

		/// <summary>
		/// Initializes a new instance of the <see cref="LazyComicInfoFallbackResolver"/> class.
		/// </summary>
		/// <param name="service">Owning details service.</param>
		/// <param name="sourceDirectoryPaths">Ordered source paths used for fallback discovery.</param>
		public LazyComicInfoFallbackResolver(
			OverrideDetailsService service,
			IReadOnlyList<string> sourceDirectoryPaths)
		{
			_service = service ?? throw new ArgumentNullException(nameof(service));
			_sourceDirectoryPaths = sourceDirectoryPaths ?? throw new ArgumentNullException(nameof(sourceDirectoryPaths));
		}

		/// <summary>
		/// Resolves fallback metadata at most once and returns the cached resolution payload.
		/// </summary>
		/// <returns>Fallback resolution payload.</returns>
		public ComicInfoFallbackResolution Resolve()
		{
			if (_hasResolution)
			{
				return _resolution;
			}

			_hasResolution = true;
			_ = _service.TryResolveComicInfoFallbackMetadata(
				_sourceDirectoryPaths,
				out ComicInfoMetadata? metadata,
				out string? comicInfoXmlPath);

			_resolution = new ComicInfoFallbackResolution(
				metadata,
				comicInfoXmlPath,
				WasResolved: true);
			return _resolution;
		}
	}

	/// <summary>
	/// Immutable payload describing one lazy fallback resolution attempt.
	/// </summary>
	/// <param name="Metadata">Resolved ComicInfo metadata when available.</param>
	/// <param name="ComicInfoXmlPath">Resolved ComicInfo.xml path when available.</param>
	/// <param name="WasResolved">Whether fallback resolution was attempted.</param>
	private readonly record struct ComicInfoFallbackResolution(
		ComicInfoMetadata? Metadata,
		string? ComicInfoXmlPath,
		bool WasResolved);
}
