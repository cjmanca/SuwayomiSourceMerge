using System.Text.Json;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Persists metadata orchestration state as a single JSON snapshot file under the configured state root.
/// </summary>
internal sealed class FileBackedMetadataStateStore : IMetadataStateStore
{
	/// <summary>
	/// Supported JSON schema version for persisted metadata state files.
	/// </summary>
	private const int SchemaVersion = 1;

	/// <summary>
	/// JSON property name for the schema version field.
	/// </summary>
	private const string SchemaVersionPropertyName = "schema_version";

	/// <summary>
	/// JSON property name for sticky FlareSolverr routing expiry timestamp.
	/// </summary>
	private const string StickyFlaresolverrUntilPropertyName = "sticky_flaresolverr_until_unix_seconds";

	/// <summary>
	/// JSON property name for per-title cooldown timestamp map.
	/// </summary>
	private const string TitleCooldownsPropertyName = "title_cooldowns_unix_seconds";

	/// <summary>
	/// Synchronization lock for state reads and mutations.
	/// </summary>
	private readonly Lock _syncRoot = new();

	/// <summary>
	/// Resolved metadata state file paths.
	/// </summary>
	private readonly MetadataStatePaths _paths;

	/// <summary>
	/// Current in-memory metadata state snapshot.
	/// </summary>
	private MetadataStateSnapshot _currentSnapshot;

	/// <summary>
	/// Initializes a new instance of the <see cref="FileBackedMetadataStateStore"/> class.
	/// </summary>
	/// <param name="paths">Resolved metadata state paths.</param>
	public FileBackedMetadataStateStore(MetadataStatePaths paths)
	{
		_paths = paths ?? throw new ArgumentNullException(nameof(paths));
		_currentSnapshot = LoadInitialSnapshot();
	}

	/// <inheritdoc />
	public MetadataStateSnapshot Read()
	{
		lock (_syncRoot)
		{
			return CloneSnapshot(_currentSnapshot);
		}
	}

	/// <inheritdoc />
	public void Transform(Func<MetadataStateSnapshot, MetadataStateSnapshot> transformer)
	{
		ArgumentNullException.ThrowIfNull(transformer);

		lock (_syncRoot)
		{
			MetadataStateSnapshot inputSnapshot = CloneSnapshot(_currentSnapshot);
			MetadataStateSnapshot? transformedSnapshot = transformer(inputSnapshot);
			ArgumentNullException.ThrowIfNull(transformedSnapshot, nameof(transformer));

			MetadataStateSnapshot normalizedSnapshot = CloneSnapshot(transformedSnapshot);
			PersistSnapshot(normalizedSnapshot);
			_currentSnapshot = normalizedSnapshot;
		}
	}

	/// <summary>
	/// Loads the initial in-memory snapshot from disk, recovering deterministically when content is malformed.
	/// </summary>
	/// <returns>Loaded metadata state snapshot.</returns>
	private MetadataStateSnapshot LoadInitialSnapshot()
	{
		Directory.CreateDirectory(_paths.StateRootPath);
		if (Directory.Exists(_paths.MetadataStateFilePath))
		{
			RecoverCorruptStateDirectory();
			return MetadataStateSnapshot.Empty;
		}

		if (!File.Exists(_paths.MetadataStateFilePath))
		{
			return MetadataStateSnapshot.Empty;
		}

		try
		{
			using FileStream stream = File.OpenRead(_paths.MetadataStateFilePath);
			using JsonDocument document = JsonDocument.Parse(stream);
			return ParseSnapshot(document.RootElement);
		}
		catch (Exception exception) when (IsCorruptStateException(exception))
		{
			RecoverCorruptStateFile();
			return MetadataStateSnapshot.Empty;
		}
	}

	/// <summary>
	/// Determines whether an exception should be treated as corrupt-state input.
	/// </summary>
	/// <param name="exception">Exception to classify.</param>
	/// <returns><see langword="true"/> when the exception indicates invalid persisted state content; otherwise <see langword="false"/>.</returns>
	private static bool IsCorruptStateException(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		return exception is JsonException
			or InvalidDataException
			or FormatException
			or OverflowException;
	}

	/// <summary>
	/// Performs best-effort backup and reset for a corrupt metadata state file.
	/// </summary>
	private void RecoverCorruptStateFile()
	{
		try
		{
			if (File.Exists(_paths.MetadataStateFilePath))
			{
				File.Copy(
					_paths.MetadataStateFilePath,
					_paths.MetadataStateCorruptFilePath,
					overwrite: true);
			}
		}
		catch
		{
			// Best-effort corrupt-state backup.
		}

		try
		{
			if (File.Exists(_paths.MetadataStateFilePath))
			{
				File.Delete(_paths.MetadataStateFilePath);
			}
		}
		catch
		{
			// Best-effort corrupt-state reset.
		}
	}

	/// <summary>
	/// Performs best-effort recovery when a directory exists at the metadata state file path.
	/// </summary>
	private void RecoverCorruptStateDirectory()
	{
		try
		{
			if (Directory.Exists(_paths.MetadataStateCorruptDirectoryPath))
			{
				Directory.Delete(_paths.MetadataStateCorruptDirectoryPath, recursive: true);
			}
		}
		catch
		{
			// Best-effort stale backup-directory cleanup.
		}

		try
		{
			if (Directory.Exists(_paths.MetadataStateFilePath))
			{
				Directory.Move(
					_paths.MetadataStateFilePath,
					_paths.MetadataStateCorruptDirectoryPath);
				return;
			}
		}
		catch
		{
			// Best-effort corrupt-directory quarantine.
		}

		try
		{
			if (Directory.Exists(_paths.MetadataStateFilePath))
			{
				Directory.Delete(_paths.MetadataStateFilePath, recursive: true);
			}
		}
		catch
		{
			// Best-effort corrupt-directory reset.
		}
	}

	/// <summary>
	/// Parses one JSON root element into a metadata state snapshot.
	/// </summary>
	/// <param name="root">Parsed JSON root element.</param>
	/// <returns>Parsed metadata state snapshot.</returns>
	private static MetadataStateSnapshot ParseSnapshot(JsonElement root)
	{
		if (root.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidDataException("Metadata state root must be a JSON object.");
		}

		int schemaVersion = ReadRequiredSchemaVersion(root);
		if (schemaVersion != SchemaVersion)
		{
			throw new InvalidDataException(
				$"Metadata state schema version '{schemaVersion}' is not supported. Expected '{SchemaVersion}'.");
		}

		DateTimeOffset? stickyFlaresolverrUntilUtc = ReadOptionalUnixSeconds(
			root,
			StickyFlaresolverrUntilPropertyName);
		Dictionary<string, DateTimeOffset> titleCooldownsUtc = ReadRequiredTitleCooldowns(root);
		return new MetadataStateSnapshot(titleCooldownsUtc, stickyFlaresolverrUntilUtc);
	}

	/// <summary>
	/// Reads and validates required schema version from one metadata state JSON document.
	/// </summary>
	/// <param name="root">JSON root element.</param>
	/// <returns>Parsed schema version value.</returns>
	private static int ReadRequiredSchemaVersion(JsonElement root)
	{
		if (!root.TryGetProperty(SchemaVersionPropertyName, out JsonElement schemaVersionElement))
		{
			throw new InvalidDataException($"Metadata state is missing required property '{SchemaVersionPropertyName}'.");
		}

		if (schemaVersionElement.ValueKind != JsonValueKind.Number ||
			!schemaVersionElement.TryGetInt32(out int schemaVersion))
		{
			throw new InvalidDataException(
				$"Metadata state property '{SchemaVersionPropertyName}' must be an integer number.");
		}

		return schemaVersion;
	}

	/// <summary>
	/// Reads and validates the optional sticky FlareSolverr expiry timestamp property.
	/// </summary>
	/// <param name="root">JSON root element.</param>
	/// <param name="propertyName">Property name to read.</param>
	/// <returns>Parsed UTC timestamp when present; otherwise <see langword="null"/>.</returns>
	private static DateTimeOffset? ReadOptionalUnixSeconds(JsonElement root, string propertyName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

		if (!root.TryGetProperty(propertyName, out JsonElement propertyElement))
		{
			return null;
		}

		if (propertyElement.ValueKind == JsonValueKind.Null)
		{
			return null;
		}

		if (propertyElement.ValueKind != JsonValueKind.Number ||
			!propertyElement.TryGetInt64(out long unixSeconds))
		{
			throw new InvalidDataException($"Metadata state property '{propertyName}' must be a unix-seconds number or null.");
		}

		return ParseUnixSeconds(unixSeconds, propertyName);
	}

	/// <summary>
	/// Reads and validates required per-title cooldown timestamps from the metadata state JSON document.
	/// </summary>
	/// <param name="root">JSON root element.</param>
	/// <returns>Parsed cooldown map keyed by normalized title key.</returns>
	private static Dictionary<string, DateTimeOffset> ReadRequiredTitleCooldowns(JsonElement root)
	{
		if (!root.TryGetProperty(TitleCooldownsPropertyName, out JsonElement cooldownsElement))
		{
			throw new InvalidDataException(
				$"Metadata state is missing required property '{TitleCooldownsPropertyName}'.");
		}

		if (cooldownsElement.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidDataException(
				$"Metadata state property '{TitleCooldownsPropertyName}' must be a JSON object.");
		}

		Dictionary<string, DateTimeOffset> cooldowns = new(StringComparer.Ordinal);
		foreach (JsonProperty property in cooldownsElement.EnumerateObject())
		{
			string titleKey = property.Name;
			if (string.IsNullOrWhiteSpace(titleKey))
			{
				throw new InvalidDataException(
					$"Metadata state property '{TitleCooldownsPropertyName}' contains an empty or whitespace key.");
			}

			string normalizedKey = titleKey.Trim();
			if (property.Value.ValueKind != JsonValueKind.Number ||
				!property.Value.TryGetInt64(out long unixSeconds))
			{
				throw new InvalidDataException(
					$"Metadata state cooldown value for key '{normalizedKey}' must be a unix-seconds number.");
			}

			DateTimeOffset parsedTimestamp = ParseUnixSeconds(
				unixSeconds,
				$"{TitleCooldownsPropertyName}.{normalizedKey}");
			if (!cooldowns.TryAdd(normalizedKey, parsedTimestamp))
			{
				throw new InvalidDataException(
					$"Metadata state cooldown key '{normalizedKey}' is duplicated.");
			}
		}

		return cooldowns;
	}

	/// <summary>
	/// Parses and validates one unix-seconds timestamp value.
	/// </summary>
	/// <param name="unixSeconds">Unix-seconds timestamp.</param>
	/// <param name="propertyName">Property name used for diagnostics.</param>
	/// <returns>Parsed UTC timestamp.</returns>
	private static DateTimeOffset ParseUnixSeconds(long unixSeconds, string propertyName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

		try
		{
			return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToUniversalTime();
		}
		catch (ArgumentOutOfRangeException exception)
		{
			throw new InvalidDataException(
				$"Metadata state property '{propertyName}' contains an out-of-range unix-seconds timestamp.",
				exception);
		}
	}

	/// <summary>
	/// Persists one metadata state snapshot using temporary-file write and atomic replacement semantics.
	/// </summary>
	/// <param name="snapshot">Snapshot to persist.</param>
	private void PersistSnapshot(MetadataStateSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		Directory.CreateDirectory(_paths.StateRootPath);
		string temporaryPath = $"{_paths.MetadataStateFilePath}.{Guid.NewGuid():N}.tmp";
		try
		{
			using (FileStream stream = File.Create(temporaryPath))
			{
				using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true });
				writer.WriteStartObject();
				writer.WriteNumber(SchemaVersionPropertyName, SchemaVersion);
				if (snapshot.StickyFlaresolverrUntilUtc is DateTimeOffset stickyFlaresolverrUntilUtc)
				{
					writer.WriteNumber(
						StickyFlaresolverrUntilPropertyName,
						stickyFlaresolverrUntilUtc.ToUniversalTime().ToUnixTimeSeconds());
				}
				else
				{
					writer.WriteNull(StickyFlaresolverrUntilPropertyName);
				}

				writer.WriteStartObject(TitleCooldownsPropertyName);
				foreach (KeyValuePair<string, DateTimeOffset> cooldown in snapshot.TitleCooldownsUtc
					.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
				{
					writer.WriteNumber(
						cooldown.Key,
						cooldown.Value.ToUniversalTime().ToUnixTimeSeconds());
				}

				writer.WriteEndObject();
				writer.WriteEndObject();
				writer.Flush();
			}

			File.Move(temporaryPath, _paths.MetadataStateFilePath, overwrite: true);
		}
		finally
		{
			TryDeleteTemporaryFile(temporaryPath);
		}
	}

	/// <summary>
	/// Deletes one temporary state-file path using best-effort semantics.
	/// </summary>
	/// <param name="temporaryPath">Temporary file path.</param>
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
		catch
		{
			// Best-effort temporary file cleanup.
		}
	}

	/// <summary>
	/// Clones a snapshot to preserve immutability boundaries across callers and internal storage.
	/// </summary>
	/// <param name="snapshot">Snapshot to clone.</param>
	/// <returns>Cloned snapshot instance.</returns>
	private static MetadataStateSnapshot CloneSnapshot(MetadataStateSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		return new MetadataStateSnapshot(
			snapshot.TitleCooldownsUtc,
			snapshot.StickyFlaresolverrUntilUtc);
	}
}
