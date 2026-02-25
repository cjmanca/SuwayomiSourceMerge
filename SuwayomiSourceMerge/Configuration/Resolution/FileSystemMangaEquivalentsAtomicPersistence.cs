using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Filesystem-backed atomic persistence implementation for <see cref="MangaEquivalentsDocument"/>.
/// </summary>
internal sealed class FileSystemMangaEquivalentsAtomicPersistence : IMangaEquivalentsAtomicPersistence
{
	/// <summary>
	/// Canonical YAML writer used for temporary-file serialization.
	/// </summary>
	private readonly YamlDocumentWriter _yamlDocumentWriter;

	/// <summary>
	/// Initializes a new instance of the <see cref="FileSystemMangaEquivalentsAtomicPersistence"/> class.
	/// </summary>
	/// <param name="yamlDocumentWriter">YAML writer dependency.</param>
	internal FileSystemMangaEquivalentsAtomicPersistence(YamlDocumentWriter yamlDocumentWriter)
	{
		_yamlDocumentWriter = yamlDocumentWriter ?? throw new ArgumentNullException(nameof(yamlDocumentWriter));
	}

	/// <inheritdoc />
	public bool TryPersistDocumentAtomically(
		string targetPath,
		MangaEquivalentsDocument document,
		out string? diagnostic)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
		ArgumentNullException.ThrowIfNull(document);

		string temporaryPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
		try
		{
			_yamlDocumentWriter.Write(temporaryPath, document);
			File.Move(temporaryPath, targetPath, overwrite: true);
			diagnostic = null;
			return true;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
		{
			diagnostic = ResolutionExceptionDiagnosticFormatter.Format(exception);
			return false;
		}
		finally
		{
			TryDeleteTemporaryFile(temporaryPath);
		}
	}

	/// <summary>
	/// Deletes one temporary file using best-effort semantics.
	/// </summary>
	/// <param name="temporaryPath">Temporary path to delete.</param>
	private static void TryDeleteTemporaryFile(string temporaryPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPath);

		try
		{
			if (File.Exists(temporaryPath))
			{
				File.Delete(temporaryPath);
			}
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
		{
			// Best-effort temporary-file cleanup.
		}
	}
}
