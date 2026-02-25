using System.Globalization;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Loading;
using SuwayomiSourceMerge.Configuration.Validation;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Validation and persistence helpers for <see cref="MangaEquivalentsUpdateService"/>.
/// </summary>
internal sealed partial class MangaEquivalentsUpdateService
{
	/// <summary>
	/// Parses and validates the manga-equivalents document using base schema rules.
	/// </summary>
	/// <param name="mangaFilePath">Manga-equivalents file path.</param>
	/// <param name="yamlContent">File content.</param>
	/// <returns>Parsed manga-equivalents document.</returns>
	private ParsedDocument<MangaEquivalentsDocument> ParseMangaEquivalents(string mangaFilePath, string yamlContent)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mangaFilePath);
		ArgumentNullException.ThrowIfNull(yamlContent);

		ConfigurationValidationPipeline pipeline = new(_yamlDocumentParser);
		return pipeline.ParseAndValidate(
			Path.GetFileName(mangaFilePath),
			yamlContent,
			new MangaEquivalentsDocumentValidator());
	}

	/// <summary>
	/// Parses and validates the scene-tags document using base schema rules.
	/// </summary>
	/// <param name="sceneTagsFilePath">Scene-tags file path.</param>
	/// <param name="yamlContent">File content.</param>
	/// <returns>Parsed scene-tags document.</returns>
	private ParsedDocument<SceneTagsDocument> ParseSceneTags(string sceneTagsFilePath, string yamlContent)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sceneTagsFilePath);
		ArgumentNullException.ThrowIfNull(yamlContent);

		ConfigurationValidationPipeline pipeline = new(_yamlDocumentParser);
		return pipeline.ParseAndValidate(
			Path.GetFileName(sceneTagsFilePath),
			yamlContent,
			new SceneTagsDocumentValidator());
	}

	/// <summary>
	/// Creates a read-failure result.
	/// </summary>
	/// <param name="mangaFilePath">Manga-equivalents file path.</param>
	/// <param name="diagnostic">Read failure diagnostic.</param>
	/// <returns>Read-failure result.</returns>
	private static MangaEquivalentsUpdateResult CreateReadFailureResult(string mangaFilePath, string? diagnostic)
	{
		return new MangaEquivalentsUpdateResult(
			MangaEquivalentsUpdateOutcome.ReadFailed,
			mangaFilePath,
			MangaEquivalentsUpdateResult.NoAffectedGroupIndex,
			0,
			diagnostic);
	}

	/// <summary>
	/// Creates a validation-failure result.
	/// </summary>
	/// <param name="mangaFilePath">Manga-equivalents file path.</param>
	/// <param name="diagnostic">Validation diagnostic.</param>
	/// <returns>Validation-failure result.</returns>
	private static MangaEquivalentsUpdateResult CreateValidationFailureResult(string mangaFilePath, string diagnostic)
	{
		return new MangaEquivalentsUpdateResult(
			MangaEquivalentsUpdateOutcome.ValidationFailed,
			mangaFilePath,
			MangaEquivalentsUpdateResult.NoAffectedGroupIndex,
			0,
			diagnostic);
	}

	/// <summary>
	/// Creates a validation-failure result from deterministic validation error entries.
	/// </summary>
	/// <param name="mangaFilePath">Manga-equivalents file path.</param>
	/// <param name="validationErrors">Validation errors.</param>
	/// <returns>Validation-failure result.</returns>
	private static MangaEquivalentsUpdateResult CreateValidationFailureResult(
		string mangaFilePath,
		IReadOnlyList<ValidationError> validationErrors)
	{
		return CreateValidationFailureResult(mangaFilePath, MergeValidationErrors(validationErrors));
	}

	/// <summary>
	/// Merges deterministic validation entries into a compact diagnostic string.
	/// </summary>
	/// <param name="first">First error list.</param>
	/// <param name="second">Second error list.</param>
	/// <returns>Combined diagnostic string.</returns>
	private static string MergeValidationErrors(IReadOnlyList<ValidationError> first, IReadOnlyList<ValidationError> second)
	{
		ArgumentNullException.ThrowIfNull(first);
		ArgumentNullException.ThrowIfNull(second);

		ValidationError[] merged = [.. first, .. second];
		return MergeValidationErrors(merged);
	}

	/// <summary>
	/// Merges deterministic validation entries into a compact diagnostic string.
	/// </summary>
	/// <param name="validationErrors">Validation errors.</param>
	/// <returns>Combined diagnostic string.</returns>
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
	/// Reads one UTF-8 text file using deterministic outcome mapping.
	/// </summary>
	/// <param name="filePath">File path.</param>
	/// <param name="content">Read content on success.</param>
	/// <param name="diagnostic">Diagnostic text on failure.</param>
	/// <returns><see langword="true"/> when read succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryReadTextFile(string filePath, out string content, out string? diagnostic)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

		try
		{
			content = File.ReadAllText(filePath);
			diagnostic = null;
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
