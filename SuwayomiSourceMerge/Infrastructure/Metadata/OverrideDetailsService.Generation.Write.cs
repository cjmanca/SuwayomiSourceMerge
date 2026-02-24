using System.Text.Json;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// details.json write helpers for <see cref="OverrideDetailsService"/>.
/// </summary>
internal sealed partial class OverrideDetailsService
{
	/// <summary>
	/// Placeholder status display values stored for shell-parity details.json output.
	/// </summary>
	private static readonly IReadOnlyList<string> _statusValueDescriptions =
	[
		"0 = Unknown",
		"1 = Ongoing",
		"2 = Completed",
		"3 = Licensed"
	];

	/// <summary>
	/// Tries to write one details.json output model without overwriting an existing destination.
	/// </summary>
	/// <param name="detailsJsonPath">Destination details.json path.</param>
	/// <param name="documentModel">details.json document model values.</param>
	/// <param name="destinationAlreadyExists">Whether destination details.json exists after a handled race or write failure.</param>
	/// <returns><see langword="true"/> when write succeeded; otherwise <see langword="false"/>.</returns>
	private static bool TryWriteDetailsJsonNonOverwriting(
		string detailsJsonPath,
		DetailsJsonDocumentModel documentModel,
		out bool destinationAlreadyExists)
	{
		try
		{
			WriteDetailsJson(detailsJsonPath, documentModel);
			destinationAlreadyExists = false;
			return true;
		}
		catch (IOException)
		{
			destinationAlreadyExists = File.Exists(detailsJsonPath);
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			destinationAlreadyExists = File.Exists(detailsJsonPath);
			return false;
		}
		catch (NotSupportedException)
		{
			destinationAlreadyExists = File.Exists(detailsJsonPath);
			return false;
		}
	}

	/// <summary>
	/// Writes details.json content to the target path using a temporary file and atomic move.
	/// </summary>
	/// <param name="detailsJsonPath">Target details.json path.</param>
	/// <param name="documentModel">Document model values.</param>
	private static void WriteDetailsJson(
		string detailsJsonPath,
		DetailsJsonDocumentModel documentModel)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(detailsJsonPath);
		ArgumentNullException.ThrowIfNull(documentModel);

		string destinationDirectory = Path.GetDirectoryName(detailsJsonPath)
			?? throw new InvalidOperationException("Details.json destination directory could not be determined.");
		Directory.CreateDirectory(destinationDirectory);

		string temporaryPath = $"{detailsJsonPath}.{Guid.NewGuid():N}.tmp";
		try
		{
			using (FileStream stream = File.Create(temporaryPath))
			{
				using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true });
				writer.WriteStartObject();
				writer.WriteString("title", documentModel.Title);
				writer.WriteString("author", documentModel.Author);
				writer.WriteString("artist", documentModel.Artist);
				writer.WriteString("description", documentModel.Description);

				writer.WriteStartArray("genre");
				for (int index = 0; index < documentModel.Genres.Count; index++)
				{
					writer.WriteStringValue(documentModel.Genres[index]);
				}

				writer.WriteEndArray();
				writer.WriteString("status", documentModel.Status);

				writer.WriteStartArray("_status values");
				for (int index = 0; index < _statusValueDescriptions.Count; index++)
				{
					writer.WriteStringValue(_statusValueDescriptions[index]);
				}

				writer.WriteEndArray();
				writer.WriteEndObject();
				writer.Flush();
			}

			File.Move(temporaryPath, detailsJsonPath, overwrite: false);
		}
		finally
		{
			TryDeleteTemporaryFile(temporaryPath);
		}
	}

	/// <summary>
	/// Tries to delete one temporary details.json path using best-effort semantics.
	/// </summary>
	/// <param name="temporaryPath">Temporary path to delete.</param>
	private static void TryDeleteTemporaryFile(string temporaryPath)
	{
		TryDeleteTemporaryFile(
			temporaryPath,
			static path => File.Exists(path),
			static path => File.Delete(path));
	}

	/// <summary>
	/// Tries to delete one temporary details.json path using provided filesystem delegates.
	/// </summary>
	/// <param name="temporaryPath">Temporary path to delete.</param>
	/// <param name="fileExists">Delegate that determines file existence for one path.</param>
	/// <param name="deleteFile">Delegate that deletes one path.</param>
	internal static void TryDeleteTemporaryFile(
		string temporaryPath,
		Func<string, bool> fileExists,
		Action<string> deleteFile)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPath);
		ArgumentNullException.ThrowIfNull(fileExists);
		ArgumentNullException.ThrowIfNull(deleteFile);

		try
		{
			if (!fileExists(temporaryPath))
			{
				return;
			}

			deleteFile(temporaryPath);
		}
		catch (IOException)
		{
			// Best-effort cleanup; ignore known filesystem failures.
		}
		catch (UnauthorizedAccessException)
		{
			// Best-effort cleanup; ignore known filesystem failures.
		}
		catch (NotSupportedException)
		{
			// Best-effort cleanup; ignore known filesystem failures.
		}
	}
}
