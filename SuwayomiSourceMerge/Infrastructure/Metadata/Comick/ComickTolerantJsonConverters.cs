using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Provides tolerant scalar extraction helpers shared by Comick converters.
/// </summary>
internal static class ComickJsonConverterValueReader
{
	/// <summary>
	/// Attempts to read a nullable integer value from one JSON element.
	/// </summary>
	/// <param name="element">Element to read.</param>
	/// <returns>Parsed integer value, or <see langword="null"/>.</returns>
	public static int? ReadNullableInt32(JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Number:
				{
					if (element.TryGetInt32(out int int32Value))
					{
						return int32Value;
					}

					if (element.TryGetInt64(out long int64Value) &&
						int64Value >= int.MinValue &&
						int64Value <= int.MaxValue)
					{
						return (int)int64Value;
					}

					return null;
				}
			case JsonValueKind.String:
				{
					string? raw = element.GetString();
					if (string.IsNullOrWhiteSpace(raw))
					{
						return null;
					}

					return int.TryParse(
						raw.Trim(),
						NumberStyles.Integer,
						CultureInfo.InvariantCulture,
						out int parsed)
						? parsed
						: null;
				}
			default:
				return null;
		}
	}

	/// <summary>
	/// Attempts to read one nullable string value from one JSON element.
	/// </summary>
	/// <param name="element">Element to read.</param>
	/// <returns>String value when available; otherwise <see langword="null"/>.</returns>
	public static string? ReadNullableString(JsonElement element)
	{
		if (element.ValueKind != JsonValueKind.String)
		{
			return null;
		}

		return element.GetString();
	}

	/// <summary>
	/// Attempts to read one string property from one JSON object.
	/// </summary>
	/// <param name="element">JSON object element.</param>
	/// <param name="propertyName">Property name.</param>
	/// <returns>Property string value when available; otherwise <see langword="null"/>.</returns>
	public static string? ReadObjectStringProperty(JsonElement element, string propertyName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
		if (element.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		return element.TryGetProperty(propertyName, out JsonElement propertyValue)
			? ReadNullableString(propertyValue)
			: null;
	}

	/// <summary>
	/// Attempts to read one nullable integer property from one JSON object.
	/// </summary>
	/// <param name="element">JSON object element.</param>
	/// <param name="propertyName">Property name.</param>
	/// <returns>Property integer value when available; otherwise <see langword="null"/>.</returns>
	public static int? ReadObjectNullableInt32Property(JsonElement element, string propertyName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
		if (element.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		return element.TryGetProperty(propertyName, out JsonElement propertyValue)
			? ReadNullableInt32(propertyValue)
			: null;
	}
}

/// <summary>
/// Provides tolerant conversion for nullable integer values.
/// </summary>
internal sealed class ComickTolerantNullableInt32Converter : JsonConverter<int?>
{
	/// <inheritdoc />
	public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		return ComickJsonConverterValueReader.ReadNullableInt32(document.RootElement);
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);
		if (value.HasValue)
		{
			writer.WriteNumberValue(value.Value);
			return;
		}

		writer.WriteNullValue();
	}
}

/// <summary>
/// Provides tolerant conversion for boolean values.
/// </summary>
internal sealed class ComickTolerantBooleanConverter : JsonConverter<bool>
{
	/// <inheritdoc />
	public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		JsonElement root = document.RootElement;
		switch (root.ValueKind)
		{
			case JsonValueKind.True:
				return true;
			case JsonValueKind.False:
				return false;
			case JsonValueKind.Number:
				{
					int? numeric = ComickJsonConverterValueReader.ReadNullableInt32(root);
					return numeric.HasValue && numeric.Value != 0;
				}
			case JsonValueKind.String:
				{
					string? raw = root.GetString();
					if (string.IsNullOrWhiteSpace(raw))
					{
						return false;
					}

					string normalized = raw.Trim();
					if (bool.TryParse(normalized, out bool parsedBoolean))
					{
						return parsedBoolean;
					}

					int? numeric = ComickJsonConverterValueReader.ReadNullableInt32(root);
					return numeric.HasValue && numeric.Value != 0;
				}
			default:
				return false;
		}
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);
		writer.WriteBooleanValue(value);
	}
}

/// <summary>
/// Provides tolerant conversion for nullable boolean values.
/// </summary>
internal sealed class ComickTolerantNullableBooleanConverter : JsonConverter<bool?>
{
	/// <inheritdoc />
	public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		JsonElement root = document.RootElement;
		switch (root.ValueKind)
		{
			case JsonValueKind.Null:
			case JsonValueKind.Undefined:
				return null;
			case JsonValueKind.True:
				return true;
			case JsonValueKind.False:
				return false;
			case JsonValueKind.Number:
				{
					int? numeric = ComickJsonConverterValueReader.ReadNullableInt32(root);
					return numeric.HasValue
						? numeric.Value != 0
						: null;
				}
			case JsonValueKind.String:
				{
					string? raw = root.GetString();
					if (string.IsNullOrWhiteSpace(raw))
					{
						return null;
					}

					string normalized = raw.Trim();
					if (bool.TryParse(normalized, out bool parsedBoolean))
					{
						return parsedBoolean;
					}

					int? numeric = ComickJsonConverterValueReader.ReadNullableInt32(root);
					return numeric.HasValue
						? numeric.Value != 0
						: null;
				}
			default:
				return null;
		}
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);
		if (value.HasValue)
		{
			writer.WriteBooleanValue(value.Value);
			return;
		}

		writer.WriteNullValue();
	}
}

/// <summary>
/// Provides tolerant conversion for non-nullable strings.
/// </summary>
internal sealed class ComickTolerantStringConverter : JsonConverter<string>
{
	/// <inheritdoc />
	public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		string? value = ComickJsonConverterValueReader.ReadNullableString(document.RootElement);
		return value ?? string.Empty;
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);
		writer.WriteStringValue(value);
	}
}

/// <summary>
/// Provides tolerant conversion for nullable strings.
/// </summary>
internal sealed class ComickTolerantNullableStringConverter : JsonConverter<string?>
{
	/// <inheritdoc />
	public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		return ComickJsonConverterValueReader.ReadNullableString(document.RootElement);
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStringValue(value);
	}
}

/// <summary>
/// Provides tolerant conversion for optional object payloads.
/// </summary>
/// <typeparam name="TValue">Object type.</typeparam>
internal sealed class ComickOptionalObjectJsonConverter<TValue> : JsonConverter<TValue?>
	where TValue : class
{
	/// <inheritdoc />
	public override TValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		JsonElement root = document.RootElement;
		if (root.ValueKind == JsonValueKind.Null || root.ValueKind == JsonValueKind.Undefined)
		{
			return null;
		}

		if (root.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		try
		{
			return root.Deserialize<TValue>(options);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, TValue? value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		JsonSerializer.Serialize(writer, value, options);
	}
}

/// <summary>
/// Provides tolerant conversion for list payloads by skipping malformed entries.
/// </summary>
/// <typeparam name="TItem">List item type.</typeparam>
internal sealed class ComickFilteredListJsonConverter<TItem> : JsonConverter<IReadOnlyList<TItem>?>
{
	/// <inheritdoc />
	public override IReadOnlyList<TItem>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		JsonElement root = document.RootElement;
		if (root.ValueKind == JsonValueKind.Null || root.ValueKind == JsonValueKind.Undefined)
		{
			return [];
		}

		List<TItem> results = [];
		if (root.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement itemElement in root.EnumerateArray())
			{
				if (TryDeserializeItem(itemElement, options, out TItem item))
				{
					results.Add(item);
				}
			}

			return results;
		}

		if (TryDeserializeItem(root, options, out TItem singleItem))
		{
			results.Add(singleItem);
		}

		return results;
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, IReadOnlyList<TItem>? value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStartArray();
		for (int index = 0; index < value.Count; index++)
		{
			TItem? item = value[index];
			if (item is null)
			{
				continue;
			}

			JsonSerializer.Serialize(writer, item, options);
		}

		writer.WriteEndArray();
	}

	/// <summary>
	/// Attempts to deserialize one list item and suppresses malformed entry exceptions.
	/// </summary>
	/// <param name="itemElement">Item element.</param>
	/// <param name="options">Serializer options.</param>
	/// <param name="item">Deserialized item when successful.</param>
	/// <returns><see langword="true"/> when item parses successfully; otherwise <see langword="false"/>.</returns>
	private static bool TryDeserializeItem(JsonElement itemElement, JsonSerializerOptions options, out TItem item)
	{
		try
		{
			TItem? parsed = itemElement.Deserialize<TItem>(options);
			if (parsed is null)
			{
				item = default!;
				return false;
			}

			item = parsed;
			return true;
		}
		catch (JsonException)
		{
			item = default!;
			return false;
		}
	}
}
