using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Provides tolerant deserialization for Comick search <c>md_titles</c> payload values.
/// </summary>
internal sealed class ComickTitleAliasListConverter : JsonConverter<IReadOnlyList<ComickTitleAlias>?>
{
	/// <summary>
	/// Reads one tolerant alias-list value.
	/// </summary>
	/// <param name="reader">JSON reader.</param>
	/// <param name="typeToConvert">Target type.</param>
	/// <param name="options">Serializer options.</param>
	/// <returns>Normalized alias list, or an empty list when value shape is invalid.</returns>
	public override IReadOnlyList<ComickTitleAlias>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		JsonElement root = document.RootElement;
		if (root.ValueKind == JsonValueKind.Array)
		{
			return ParseAliasArray(root);
		}

		if (TryCreateAlias(root, out ComickTitleAlias alias))
		{
			return [alias];
		}

		return [];
	}

	/// <summary>
	/// Writes one alias-list value.
	/// </summary>
	/// <param name="writer">JSON writer.</param>
	/// <param name="value">Alias list value.</param>
	/// <param name="options">Serializer options.</param>
	public override void Write(Utf8JsonWriter writer, IReadOnlyList<ComickTitleAlias>? value, JsonSerializerOptions options)
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
			ComickTitleAlias? alias = value[index];
			if (alias is null || string.IsNullOrWhiteSpace(alias.Title))
			{
				continue;
			}

			writer.WriteStartObject();
			writer.WriteString("title", alias.Title);
			if (!string.IsNullOrWhiteSpace(alias.Language))
			{
				writer.WriteString("lang", alias.Language);
			}

			writer.WriteEndObject();
		}

		writer.WriteEndArray();
	}

	/// <summary>
	/// Parses one array payload into normalized alias entries.
	/// </summary>
	/// <param name="arrayElement">Alias array element.</param>
	/// <returns>Normalized alias list.</returns>
	private static IReadOnlyList<ComickTitleAlias> ParseAliasArray(JsonElement arrayElement)
	{
		List<ComickTitleAlias> aliases = [];
		foreach (JsonElement item in arrayElement.EnumerateArray())
		{
			if (TryCreateAlias(item, out ComickTitleAlias alias))
			{
				aliases.Add(alias);
			}
		}

		return aliases;
	}

	/// <summary>
	/// Attempts to convert one payload token into a normalized alias entry.
	/// </summary>
	/// <param name="element">Payload token.</param>
	/// <param name="alias">Normalized alias when conversion succeeds.</param>
	/// <returns><see langword="true"/> when conversion succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryCreateAlias(JsonElement element, out ComickTitleAlias alias)
	{
		alias = new ComickTitleAlias();
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				return TryCreateAliasFromObject(element, out alias);
			case JsonValueKind.String:
				{
					string? scalarTitle = element.GetString();
					if (string.IsNullOrWhiteSpace(scalarTitle))
					{
						return false;
					}

					alias = new ComickTitleAlias
					{
						Title = scalarTitle.Trim()
					};
					return true;
				}
			default:
				return false;
		}
	}

	/// <summary>
	/// Attempts to convert one object token into a normalized alias entry.
	/// </summary>
	/// <param name="element">Object token.</param>
	/// <param name="alias">Normalized alias when conversion succeeds.</param>
	/// <returns><see langword="true"/> when conversion succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryCreateAliasFromObject(JsonElement element, out ComickTitleAlias alias)
	{
		alias = new ComickTitleAlias();
		string? title = null;
		string? language = null;
		if (element.TryGetProperty("title", out JsonElement titleElement) &&
			titleElement.ValueKind == JsonValueKind.String)
		{
			title = titleElement.GetString()?.Trim();
		}

		if (string.IsNullOrWhiteSpace(title))
		{
			return false;
		}

		if (element.TryGetProperty("lang", out JsonElement languageElement) &&
			languageElement.ValueKind == JsonValueKind.String)
		{
			language = languageElement.GetString()?.Trim();
			if (string.IsNullOrWhiteSpace(language))
			{
				language = null;
			}
		}

		alias = new ComickTitleAlias
		{
			Title = title,
			Language = language
		};
		return true;
	}
}
