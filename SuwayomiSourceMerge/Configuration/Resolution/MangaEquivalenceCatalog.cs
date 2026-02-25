using System.Globalization;
using System.Threading;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Loading;
using SuwayomiSourceMerge.Configuration.Validation;
using SuwayomiSourceMerge.Domain.Normalization;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Maintains a mutable runtime manga-equivalence catalog backed by immutable resolver snapshots.
/// </summary>
/// <remarks>
/// The catalog delegates read operations to the current immutable snapshot and atomically swaps to a new
/// snapshot only after updater persistence succeeds and persisted YAML reload/rebuild succeeds.
/// </remarks>
internal sealed class MangaEquivalenceCatalog : IMangaEquivalenceCatalog
{
	/// <summary>
	/// Synchronization gate for catalog update operations.
	/// </summary>
	private readonly object _catalogUpdateLock = new();

	/// <summary>
	/// Scene-tag matcher reused for resolver rebuild parity.
	/// </summary>
	private readonly ISceneTagMatcher _sceneTagMatcher;

	/// <summary>
	/// Updater dependency used for deterministic YAML mutation and persistence.
	/// </summary>
	private readonly IMangaEquivalentsUpdateService _mangaEquivalentsUpdateService;

	/// <summary>
	/// YAML parser dependency used for persisted catalog reload.
	/// </summary>
	private readonly YamlDocumentParser _yamlDocumentParser;

	/// <summary>
	/// Current immutable resolver snapshot.
	/// </summary>
	private IMangaEquivalenceService _currentSnapshot;

	/// <summary>
	/// Persisted manga-equivalents path pending runtime snapshot reload after a previous reload failure.
	/// </summary>
	private string? _pendingReloadMangaEquivalentsYamlPath;

	/// <summary>
	/// Initializes a new instance of the <see cref="MangaEquivalenceCatalog"/> class.
	/// </summary>
	/// <param name="document">Initial manga-equivalents document loaded during startup.</param>
	/// <param name="sceneTagMatcher">Scene-tag matcher used for resolver normalization behavior.</param>
	public MangaEquivalenceCatalog(MangaEquivalentsDocument document, ISceneTagMatcher sceneTagMatcher)
		: this(
			document,
			ThrowIfNullSceneTagMatcher(sceneTagMatcher),
			CreateUpdateServiceWithPinnedSceneTagMatcher(sceneTagMatcher),
			new YamlDocumentParser())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MangaEquivalenceCatalog"/> class.
	/// </summary>
	/// <param name="document">Initial manga-equivalents document loaded during startup.</param>
	/// <param name="sceneTagMatcher">Scene-tag matcher used for resolver normalization behavior.</param>
	/// <param name="mangaEquivalentsUpdateService">Updater dependency used for deterministic persistence behavior.</param>
	/// <param name="yamlDocumentParser">YAML parser dependency used for persisted catalog reload.</param>
	internal MangaEquivalenceCatalog(
		MangaEquivalentsDocument document,
		ISceneTagMatcher sceneTagMatcher,
		IMangaEquivalentsUpdateService mangaEquivalentsUpdateService,
		YamlDocumentParser yamlDocumentParser)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(sceneTagMatcher);
		ArgumentNullException.ThrowIfNull(mangaEquivalentsUpdateService);
		ArgumentNullException.ThrowIfNull(yamlDocumentParser);

		_sceneTagMatcher = sceneTagMatcher;
		_mangaEquivalentsUpdateService = mangaEquivalentsUpdateService;
		_yamlDocumentParser = yamlDocumentParser;
		_currentSnapshot = new MangaEquivalenceService(document, _sceneTagMatcher);
	}

	/// <inheritdoc />
	public bool TryResolveCanonicalTitle(string inputTitle, out string canonicalTitle)
	{
		IMangaEquivalenceService snapshot = Volatile.Read(ref _currentSnapshot);
		return snapshot.TryResolveCanonicalTitle(inputTitle, out canonicalTitle);
	}

	/// <inheritdoc />
	public string ResolveCanonicalOrInput(string inputTitle)
	{
		IMangaEquivalenceService snapshot = Volatile.Read(ref _currentSnapshot);
		return snapshot.ResolveCanonicalOrInput(inputTitle);
	}

	/// <inheritdoc />
	public MangaEquivalenceCatalogUpdateResult Update(MangaEquivalentsUpdateRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		lock (_catalogUpdateLock)
		{
			MangaEquivalentsUpdateResult updateResult;
			try
			{
				updateResult = _mangaEquivalentsUpdateService.Update(request);
			}
			catch (Exception exception) when (!IsFatalException(exception))
			{
				updateResult = new MangaEquivalentsUpdateResult(
					MangaEquivalentsUpdateOutcome.UnhandledException,
					request.MangaEquivalentsYamlPath,
					MangaEquivalentsUpdateResult.NoAffectedGroupIndex,
					0,
					ResolutionExceptionDiagnosticFormatter.Format(exception));
				return new MangaEquivalenceCatalogUpdateResult(
					MangaEquivalenceCatalogUpdateOutcome.UpdateFailed,
					updateResult,
					updateResult.Diagnostic);
			}

			if (updateResult.Outcome == MangaEquivalentsUpdateOutcome.NoChanges)
			{
				if (!string.IsNullOrWhiteSpace(_pendingReloadMangaEquivalentsYamlPath))
				{
					if (!TryBuildReloadedSnapshot(
						_pendingReloadMangaEquivalentsYamlPath,
						out IMangaEquivalenceService pendingReloadedSnapshot,
						out string pendingReloadDiagnostic))
					{
						return new MangaEquivalenceCatalogUpdateResult(
							MangaEquivalenceCatalogUpdateOutcome.ReloadFailed,
							updateResult,
							pendingReloadDiagnostic);
					}

					Volatile.Write(ref _currentSnapshot, pendingReloadedSnapshot);
					_pendingReloadMangaEquivalentsYamlPath = null;
				}

				return new MangaEquivalenceCatalogUpdateResult(
					MangaEquivalenceCatalogUpdateOutcome.NoChanges,
					updateResult,
					updateResult.Diagnostic);
			}

			if (!IsPersistedSuccess(updateResult.Outcome))
			{
				return new MangaEquivalenceCatalogUpdateResult(
					MangaEquivalenceCatalogUpdateOutcome.UpdateFailed,
					updateResult,
					updateResult.Diagnostic);
			}

			if (!TryBuildReloadedSnapshot(
				updateResult.MangaEquivalentsYamlPath,
				out IMangaEquivalenceService reloadedSnapshot,
				out string reloadDiagnostic))
			{
				_pendingReloadMangaEquivalentsYamlPath = updateResult.MangaEquivalentsYamlPath;
				return new MangaEquivalenceCatalogUpdateResult(
					MangaEquivalenceCatalogUpdateOutcome.ReloadFailed,
					updateResult,
					reloadDiagnostic);
			}

			Volatile.Write(ref _currentSnapshot, reloadedSnapshot);
			_pendingReloadMangaEquivalentsYamlPath = null;
			return new MangaEquivalenceCatalogUpdateResult(
				MangaEquivalenceCatalogUpdateOutcome.Applied,
				updateResult,
				diagnostic: null);
		}
	}

	/// <summary>
	/// Determines whether updater output indicates successful persistence.
	/// </summary>
	/// <param name="outcome">Updater outcome.</param>
	/// <returns><see langword="true"/> when persistence succeeded; otherwise <see langword="false"/>.</returns>
	private static bool IsPersistedSuccess(MangaEquivalentsUpdateOutcome outcome)
	{
		return outcome == MangaEquivalentsUpdateOutcome.UpdatedExistingGroup
			|| outcome == MangaEquivalentsUpdateOutcome.CreatedNewGroup;
	}

	/// <summary>
	/// Creates an updater pinned to one startup scene-tag matcher.
	/// </summary>
	/// <param name="sceneTagMatcher">Startup scene-tag matcher.</param>
	/// <returns>Updater instance pinned to the provided matcher.</returns>
	private static IMangaEquivalentsUpdateService CreateUpdateServiceWithPinnedSceneTagMatcher(
		ISceneTagMatcher sceneTagMatcher)
	{
		ArgumentNullException.ThrowIfNull(sceneTagMatcher);
		return new MangaEquivalentsUpdateService(sceneTagMatcher);
	}

	/// <summary>
	/// Validates required startup scene-tag matcher dependencies.
	/// </summary>
	/// <param name="sceneTagMatcher">Scene-tag matcher dependency.</param>
	/// <returns>Validated scene-tag matcher dependency.</returns>
	private static ISceneTagMatcher ThrowIfNullSceneTagMatcher(ISceneTagMatcher sceneTagMatcher)
	{
		ArgumentNullException.ThrowIfNull(sceneTagMatcher);
		return sceneTagMatcher;
	}

	/// <summary>
	/// Attempts to build one immutable resolver snapshot from persisted YAML.
	/// </summary>
	/// <param name="mangaEquivalentsYamlPath">Persisted manga-equivalents YAML path.</param>
	/// <param name="reloadedSnapshot">Reloaded resolver snapshot on success.</param>
	/// <param name="diagnostic">Deterministic diagnostic on failure.</param>
	/// <returns><see langword="true"/> when reload and rebuild succeed; otherwise <see langword="false"/>.</returns>
	private bool TryBuildReloadedSnapshot(
		string mangaEquivalentsYamlPath,
		out IMangaEquivalenceService reloadedSnapshot,
		out string diagnostic)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mangaEquivalentsYamlPath);

		reloadedSnapshot = null!;
		if (!TryReadTextFile(mangaEquivalentsYamlPath, out string yamlContent, out string readDiagnostic))
		{
			diagnostic = readDiagnostic;
			return false;
		}

		string fileName = Path.GetFileName(mangaEquivalentsYamlPath);
		ParsedDocument<MangaEquivalentsDocument> parsedDocument = ParseMangaEquivalents(fileName, yamlContent);
		if (!parsedDocument.Validation.IsValid || parsedDocument.Document is null)
		{
			diagnostic = parsedDocument.Validation.Errors.Count == 0
				? "Persisted manga-equivalents YAML reload produced invalid parse state."
				: MergeValidationErrors(parsedDocument.Validation.Errors);
			return false;
		}

		ValidationResult sceneTagAwareValidation = new MangaEquivalentsDocumentValidator(_sceneTagMatcher)
			.Validate(parsedDocument.Document, fileName);
		if (!sceneTagAwareValidation.IsValid)
		{
			diagnostic = MergeValidationErrors(sceneTagAwareValidation.Errors);
			return false;
		}

		try
		{
			reloadedSnapshot = new MangaEquivalenceService(parsedDocument.Document, _sceneTagMatcher);
			diagnostic = string.Empty;
			return true;
		}
		catch (Exception exception) when (!IsFatalException(exception))
		{
			diagnostic = ResolutionExceptionDiagnosticFormatter.Format(exception);
			return false;
		}
	}

	/// <summary>
	/// Determines whether an exception should be treated as fatal and rethrown.
	/// </summary>
	/// <param name="exception">Exception to inspect.</param>
	/// <returns><see langword="true"/> when exception is fatal; otherwise <see langword="false"/>.</returns>
	private static bool IsFatalException(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return exception is OutOfMemoryException
			|| exception is StackOverflowException
			|| exception is AccessViolationException;
	}

	/// <summary>
	/// Parses and validates one manga-equivalents YAML payload using base schema rules.
	/// </summary>
	/// <param name="fileName">Logical file name used in validation diagnostics.</param>
	/// <param name="yamlContent">YAML content.</param>
	/// <returns>Parsed document payload.</returns>
	private ParsedDocument<MangaEquivalentsDocument> ParseMangaEquivalents(string fileName, string yamlContent)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
		ArgumentNullException.ThrowIfNull(yamlContent);

		ConfigurationValidationPipeline pipeline = new(_yamlDocumentParser);
		return pipeline.ParseAndValidate(
			fileName,
			yamlContent,
			new MangaEquivalentsDocumentValidator());
	}

	/// <summary>
	/// Merges deterministic validation errors into one compact diagnostic string.
	/// </summary>
	/// <param name="validationErrors">Validation errors.</param>
	/// <returns>Combined deterministic diagnostic text.</returns>
	private static string MergeValidationErrors(IReadOnlyList<ValidationError> validationErrors)
	{
		ArgumentNullException.ThrowIfNull(validationErrors);

		return string.Join(
			" | ",
			validationErrors
				.OrderBy(static error => error.File, StringComparer.Ordinal)
				.ThenBy(static error => error.Path, StringComparer.Ordinal)
				.ThenBy(static error => error.Code, StringComparer.Ordinal)
				.ThenBy(static error => error.Message, StringComparer.Ordinal)
				.Select(
					static error => string.Create(
						CultureInfo.InvariantCulture,
						$"{error.File}:{error.Path}:{error.Code} {error.Message}")));
	}

	/// <summary>
	/// Reads one UTF-8 text file with deterministic diagnostics.
	/// </summary>
	/// <param name="filePath">File path.</param>
	/// <param name="content">Read content on success.</param>
	/// <param name="diagnostic">Deterministic diagnostic text on failure.</param>
	/// <returns><see langword="true"/> when read succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryReadTextFile(string filePath, out string content, out string diagnostic)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

		try
		{
			content = File.ReadAllText(filePath);
			diagnostic = string.Empty;
			return true;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
		{
			content = string.Empty;
			diagnostic = ResolutionExceptionDiagnosticFormatter.Format(exception);
			return false;
		}
	}
}
