using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Loading;
using SuwayomiSourceMerge.Configuration.Validation;
using SuwayomiSourceMerge.Domain.Normalization;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Updates <c>manga_equivalents.yml</c> using deterministic merge rules for Comick main and alternate titles.
/// </summary>
/// <remarks>
/// This service enforces strict scene-tag-aware validation prior to persistence and writes changes atomically.
/// </remarks>
internal sealed partial class MangaEquivalentsUpdateService : IMangaEquivalentsUpdateService
{
	/// <summary>
	/// Canonical scene-tags file name expected under the same configuration root as <c>manga_equivalents.yml</c>.
	/// </summary>
	private const string SceneTagsFileName = "scene_tags.yml";

	/// <summary>
	/// YAML parser used for deterministic document parsing.
	/// </summary>
	private readonly YamlDocumentParser _yamlDocumentParser;

	/// <summary>
	/// Atomic persistence dependency used for write-stage result mapping.
	/// </summary>
	private readonly IMangaEquivalentsAtomicPersistence _atomicPersistence;

	/// <summary>
	/// Initializes a new instance of the <see cref="MangaEquivalentsUpdateService"/> class.
	/// </summary>
	public MangaEquivalentsUpdateService()
		: this(
			new YamlDocumentParser(),
			new FileSystemMangaEquivalentsAtomicPersistence(new Configuration.Bootstrap.YamlDocumentWriter()))
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MangaEquivalentsUpdateService"/> class.
	/// </summary>
	/// <param name="yamlDocumentParser">YAML parser dependency.</param>
	/// <param name="atomicPersistence">Atomic persistence dependency.</param>
	internal MangaEquivalentsUpdateService(
		YamlDocumentParser yamlDocumentParser,
		IMangaEquivalentsAtomicPersistence atomicPersistence)
	{
		_yamlDocumentParser = yamlDocumentParser ?? throw new ArgumentNullException(nameof(yamlDocumentParser));
		_atomicPersistence = atomicPersistence ?? throw new ArgumentNullException(nameof(atomicPersistence));
	}

	/// <inheritdoc />
	public MangaEquivalentsUpdateResult Update(MangaEquivalentsUpdateRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		string mangaFilePath = request.MangaEquivalentsYamlPath;
		string configRootPath = Path.GetDirectoryName(mangaFilePath) ?? string.Empty;
		string sceneTagsFilePath = Path.GetFullPath(Path.Combine(configRootPath, SceneTagsFileName));

		if (!TryReadTextFile(mangaFilePath, out string mangaYamlContent, out string? mangaReadDiagnostic))
		{
			return CreateReadFailureResult(mangaFilePath, mangaReadDiagnostic);
		}

		if (!TryReadTextFile(sceneTagsFilePath, out string sceneTagsYamlContent, out string? sceneTagsReadDiagnostic))
		{
			return CreateReadFailureResult(
				mangaFilePath,
				$"Failed to read scene tags from '{sceneTagsFilePath}'. {sceneTagsReadDiagnostic}");
		}

		ParsedDocument<MangaEquivalentsDocument> parsedManga = ParseMangaEquivalents(mangaFilePath, mangaYamlContent);
		ParsedDocument<SceneTagsDocument> parsedSceneTags = ParseSceneTags(sceneTagsFilePath, sceneTagsYamlContent);
		if (!parsedManga.Validation.IsValid || parsedManga.Document is null ||
			!parsedSceneTags.Validation.IsValid || parsedSceneTags.Document is null)
		{
			return CreateValidationFailureResult(
				mangaFilePath,
				MergeValidationErrors(parsedManga.Validation.Errors, parsedSceneTags.Validation.Errors));
		}

		SceneTagMatcher sceneTagMatcher = new(parsedSceneTags.Document.Tags ?? []);
		ITitleComparisonNormalizer titleComparisonNormalizer = TitleComparisonNormalizerProvider.Get(sceneTagMatcher);

		List<IncomingTitleCandidate> dedupedIncomingTitles = BuildDedupedIncomingTitles(request, titleComparisonNormalizer);
		List<IncomingTitleCandidate> canonicalSelectionAlternates = BuildCanonicalSelectionAlternates(request);
		if (dedupedIncomingTitles.Count == 0)
		{
			return CreateValidationFailureResult(
				mangaFilePath,
				"No incoming title values produced non-empty normalized keys.");
		}

		MangaEquivalentsDocument updatedDocument = CloneDocument(parsedManga.Document);
		HashSet<int> matchedGroupIndices = FindMatchedGroupIndices(updatedDocument, dedupedIncomingTitles, titleComparisonNormalizer);
		if (matchedGroupIndices.Count > 1)
		{
			return new MangaEquivalentsUpdateResult(
				MangaEquivalentsUpdateOutcome.Conflict,
				mangaFilePath,
				MangaEquivalentsUpdateResult.NoAffectedGroupIndex,
				0,
				$"Incoming titles matched multiple groups ({string.Join(",", matchedGroupIndices.OrderBy(static index => index))}).");
		}

		MangaEquivalentsUpdateOutcome plannedOutcome;
		int affectedGroupIndex;
		int addedAliasCount;
		if (matchedGroupIndices.Count == 0)
		{
			string canonicalTitle = SelectCanonicalTitle(
				request.PreferredLanguage,
				request.MainTitle,
				canonicalSelectionAlternates);
			(affectedGroupIndex, addedAliasCount) = CreateNewGroup(
				updatedDocument,
				dedupedIncomingTitles,
				titleComparisonNormalizer,
				canonicalTitle);
			plannedOutcome = MangaEquivalentsUpdateOutcome.CreatedNewGroup;
		}
		else
		{
			affectedGroupIndex = matchedGroupIndices.Single();
			addedAliasCount = AppendMissingAliases(updatedDocument, affectedGroupIndex, dedupedIncomingTitles, titleComparisonNormalizer);
			plannedOutcome = addedAliasCount > 0
				? MangaEquivalentsUpdateOutcome.UpdatedExistingGroup
				: MangaEquivalentsUpdateOutcome.NoChanges;
		}

		ValidationResult sceneTagAwareValidation = new MangaEquivalentsDocumentValidator(sceneTagMatcher)
			.Validate(updatedDocument, Path.GetFileName(mangaFilePath));
		if (!sceneTagAwareValidation.IsValid)
		{
			return CreateValidationFailureResult(mangaFilePath, sceneTagAwareValidation.Errors);
		}

		if (plannedOutcome == MangaEquivalentsUpdateOutcome.NoChanges)
		{
			return new MangaEquivalentsUpdateResult(
				MangaEquivalentsUpdateOutcome.NoChanges,
				mangaFilePath,
				affectedGroupIndex,
				0,
				diagnostic: null);
		}

		if (!_atomicPersistence.TryPersistDocumentAtomically(mangaFilePath, updatedDocument, out string? writeDiagnostic))
		{
			return new MangaEquivalentsUpdateResult(
				MangaEquivalentsUpdateOutcome.WriteFailed,
				mangaFilePath,
				affectedGroupIndex,
				addedAliasCount,
				writeDiagnostic);
		}

		return new MangaEquivalentsUpdateResult(
			plannedOutcome,
			mangaFilePath,
			affectedGroupIndex,
			addedAliasCount,
			diagnostic: null);
	}
}
