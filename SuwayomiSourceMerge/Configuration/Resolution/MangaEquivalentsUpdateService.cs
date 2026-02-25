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
	/// Optional startup scene-tag matcher override used to keep runtime update behavior in-process consistent.
	/// </summary>
	private readonly ISceneTagMatcher? _sceneTagMatcherOverride;

	/// <summary>
	/// Initializes a new instance of the <see cref="MangaEquivalentsUpdateService"/> class.
	/// </summary>
	public MangaEquivalentsUpdateService()
		: this(
			new YamlDocumentParser(),
			new FileSystemMangaEquivalentsAtomicPersistence(new Configuration.Bootstrap.YamlDocumentWriter()),
			sceneTagMatcherOverride: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MangaEquivalentsUpdateService"/> class.
	/// </summary>
	/// <param name="sceneTagMatcherOverride">
	/// Startup scene-tag matcher override used for runtime update consistency.
	/// When provided, update processing does not reload <c>scene_tags.yml</c> from disk.
	/// </param>
	internal MangaEquivalentsUpdateService(ISceneTagMatcher sceneTagMatcherOverride)
		: this(
			new YamlDocumentParser(),
			new FileSystemMangaEquivalentsAtomicPersistence(new Configuration.Bootstrap.YamlDocumentWriter()),
			ThrowIfNullSceneTagMatcherOverride(sceneTagMatcherOverride))
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
		: this(
			yamlDocumentParser,
			atomicPersistence,
			sceneTagMatcherOverride: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MangaEquivalentsUpdateService"/> class.
	/// </summary>
	/// <param name="yamlDocumentParser">YAML parser dependency.</param>
	/// <param name="atomicPersistence">Atomic persistence dependency.</param>
	/// <param name="sceneTagMatcherOverride">
	/// Optional startup scene-tag matcher override.
	/// When provided, update processing skips disk-based scene-tags reload.
	/// </param>
	internal MangaEquivalentsUpdateService(
		YamlDocumentParser yamlDocumentParser,
		IMangaEquivalentsAtomicPersistence atomicPersistence,
		ISceneTagMatcher? sceneTagMatcherOverride)
	{
		_yamlDocumentParser = yamlDocumentParser ?? throw new ArgumentNullException(nameof(yamlDocumentParser));
		_atomicPersistence = atomicPersistence ?? throw new ArgumentNullException(nameof(atomicPersistence));
		_sceneTagMatcherOverride = sceneTagMatcherOverride;
	}

	/// <inheritdoc />
	public MangaEquivalentsUpdateResult Update(MangaEquivalentsUpdateRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		string mangaFilePath = request.MangaEquivalentsYamlPath;

		if (!TryReadTextFile(mangaFilePath, out string mangaYamlContent, out string? mangaReadDiagnostic))
		{
			return CreateReadFailureResult(mangaFilePath, mangaReadDiagnostic);
		}

		ParsedDocument<MangaEquivalentsDocument> parsedManga = ParseMangaEquivalents(mangaFilePath, mangaYamlContent);
		if (!parsedManga.Validation.IsValid || parsedManga.Document is null)
		{
			return CreateValidationFailureResult(
				mangaFilePath,
				parsedManga.Validation.Errors);
		}

		if (!TryResolveSceneTagMatcherForUpdate(mangaFilePath, out ISceneTagMatcher sceneTagMatcher, out MangaEquivalentsUpdateResult? failureResult))
		{
			return failureResult!;
		}

		ITitleComparisonNormalizer titleComparisonNormalizer = TitleComparisonNormalizerProvider.Get(sceneTagMatcher);

		List<IncomingTitleCandidate> dedupedIncomingTitles = BuildDedupedIncomingTitles(request, titleComparisonNormalizer);
		List<IncomingTitleCandidate> canonicalSelectionAlternates = BuildCanonicalSelectionAlternates(request, titleComparisonNormalizer);
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

	/// <summary>
	/// Resolves the scene-tag matcher used for update normalization and validation.
	/// </summary>
	/// <param name="mangaFilePath">Manga-equivalents YAML path.</param>
	/// <param name="sceneTagMatcher">Resolved matcher on success.</param>
	/// <param name="failureResult">Deterministic failure result on failure.</param>
	/// <returns><see langword="true"/> when matcher resolution succeeds; otherwise <see langword="false"/>.</returns>
	private bool TryResolveSceneTagMatcherForUpdate(
		string mangaFilePath,
		out ISceneTagMatcher sceneTagMatcher,
		out MangaEquivalentsUpdateResult? failureResult)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mangaFilePath);

		if (_sceneTagMatcherOverride is not null)
		{
			sceneTagMatcher = _sceneTagMatcherOverride;
			failureResult = null;
			return true;
		}

		string configRootPath = Path.GetDirectoryName(mangaFilePath) ?? string.Empty;
		string sceneTagsFilePath = Path.GetFullPath(Path.Combine(configRootPath, SceneTagsFileName));
		if (!TryReadTextFile(sceneTagsFilePath, out string sceneTagsYamlContent, out string? sceneTagsReadDiagnostic))
		{
			sceneTagMatcher = null!;
			failureResult = CreateReadFailureResult(
				mangaFilePath,
				$"Failed to read scene tags from '{sceneTagsFilePath}'. {sceneTagsReadDiagnostic}");
			return false;
		}

		ParsedDocument<SceneTagsDocument> parsedSceneTags = ParseSceneTags(sceneTagsFilePath, sceneTagsYamlContent);
		if (!parsedSceneTags.Validation.IsValid || parsedSceneTags.Document is null)
		{
			sceneTagMatcher = null!;
			failureResult = CreateValidationFailureResult(
				mangaFilePath,
				parsedSceneTags.Validation.Errors);
			return false;
		}

		sceneTagMatcher = new SceneTagMatcher(parsedSceneTags.Document.Tags ?? []);
		failureResult = null;
		return true;
	}

	/// <summary>
	/// Validates required startup scene-tag matcher overrides and returns the validated value.
	/// </summary>
	/// <param name="sceneTagMatcherOverride">Startup scene-tag matcher override.</param>
	/// <returns>Validated startup scene-tag matcher override.</returns>
	private static ISceneTagMatcher ThrowIfNullSceneTagMatcherOverride(ISceneTagMatcher sceneTagMatcherOverride)
	{
		ArgumentNullException.ThrowIfNull(sceneTagMatcherOverride);
		return sceneTagMatcherOverride;
	}
}
