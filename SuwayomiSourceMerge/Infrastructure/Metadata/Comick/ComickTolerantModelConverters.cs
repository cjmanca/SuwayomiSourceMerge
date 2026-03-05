using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Provides tolerant conversion for creator entries.
/// </summary>
internal sealed class ComickCreatorJsonConverter : JsonConverter<ComickCreator>
{
	/// <inheritdoc />
	public override ComickCreator Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		JsonElement root = document.RootElement;
		if (root.ValueKind == JsonValueKind.String)
		{
			string? scalarName = root.GetString();
			if (string.IsNullOrWhiteSpace(scalarName))
			{
				throw new JsonException("Creator string value was empty.");
			}

			return new ComickCreator
			{
				Name = scalarName.Trim(),
				Slug = string.Empty
			};
		}

		if (root.ValueKind != JsonValueKind.Object)
		{
			throw new JsonException("Creator value must be an object or string.");
		}

		string name = ComickJsonConverterValueReader.ReadObjectStringProperty(root, "name") ?? string.Empty;
		string slug = ComickJsonConverterValueReader.ReadObjectStringProperty(root, "slug") ?? string.Empty;
		if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(slug))
		{
			throw new JsonException("Creator object did not contain usable name or slug.");
		}

		return new ComickCreator
		{
			Name = name,
			Slug = slug
		};
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, ComickCreator value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(value);

		writer.WriteStartObject();
		writer.WriteString("name", value.Name);
		writer.WriteString("slug", value.Slug);
		writer.WriteEndObject();
	}
}

/// <summary>
/// Provides tolerant conversion for cover entries.
/// </summary>
internal sealed class ComickCoverJsonConverter : JsonConverter<ComickCover>
{
	/// <inheritdoc />
	public override ComickCover Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		JsonElement root = document.RootElement;
		if (root.ValueKind == JsonValueKind.String)
		{
			string? scalarKey = root.GetString();
			if (string.IsNullOrWhiteSpace(scalarKey))
			{
				throw new JsonException("Cover string value was empty.");
			}

			return new ComickCover
			{
				B2Key = scalarKey.Trim()
			};
		}

		if (root.ValueKind != JsonValueKind.Object)
		{
			throw new JsonException("Cover value must be an object or string.");
		}

		string b2Key = ComickJsonConverterValueReader.ReadObjectStringProperty(root, "b2key") ?? string.Empty;
		if (string.IsNullOrWhiteSpace(b2Key))
		{
			throw new JsonException("Cover object did not contain a usable b2key.");
		}

		int? width = ComickJsonConverterValueReader.ReadObjectNullableInt32Property(root, "w");
		int? height = ComickJsonConverterValueReader.ReadObjectNullableInt32Property(root, "h");
		return new ComickCover
		{
			Volume = ComickJsonConverterValueReader.ReadObjectStringProperty(root, "vol"),
			Width = width,
			Height = height,
			B2Key = b2Key
		};
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, ComickCover value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(value);

		writer.WriteStartObject();
		if (!string.IsNullOrWhiteSpace(value.Volume))
		{
			writer.WriteString("vol", value.Volume);
		}

		if (value.Width.HasValue)
		{
			writer.WriteNumber("w", value.Width.Value);
		}

		if (value.Height.HasValue)
		{
			writer.WriteNumber("h", value.Height.Value);
		}

		writer.WriteString("b2key", value.B2Key);
		writer.WriteEndObject();
	}
}

/// <summary>
/// Provides tolerant conversion for genre mapping entries.
/// </summary>
internal sealed class ComickComicGenreMappingJsonConverter : JsonConverter<ComickComicGenreMapping>
{
	/// <inheritdoc />
	public override ComickComicGenreMapping Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		JsonElement root = document.RootElement;
		if (root.ValueKind != JsonValueKind.Object ||
			!root.TryGetProperty("md_genres", out JsonElement genreElement))
		{
			throw new JsonException("Genre mapping must contain md_genres.");
		}

		ComickGenreDescriptor descriptor = genreElement.ValueKind switch
		{
			JsonValueKind.Object => new ComickGenreDescriptor
			{
				Name = ComickJsonConverterValueReader.ReadObjectStringProperty(genreElement, "name") ?? string.Empty,
				Type = ComickJsonConverterValueReader.ReadObjectStringProperty(genreElement, "type"),
				Slug = ComickJsonConverterValueReader.ReadObjectStringProperty(genreElement, "slug") ?? string.Empty,
				Group = ComickJsonConverterValueReader.ReadObjectStringProperty(genreElement, "group")
			},
			JsonValueKind.String => new ComickGenreDescriptor
			{
				Name = genreElement.GetString() ?? string.Empty
			},
			_ => throw new JsonException("Genre mapping md_genres must be object or string.")
		};
		if (string.IsNullOrWhiteSpace(descriptor.Name))
		{
			throw new JsonException("Genre mapping did not contain a usable genre name.");
		}

		return new ComickComicGenreMapping
		{
			Genre = descriptor
		};
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, ComickComicGenreMapping value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(value);

		writer.WriteStartObject();
		if (value.Genre is not null)
		{
			writer.WritePropertyName("md_genres");
			JsonSerializer.Serialize(writer, value.Genre, options);
		}

		writer.WriteEndObject();
	}
}

/// <summary>
/// Provides tolerant conversion for MangaUpdates category descriptors.
/// </summary>
internal sealed class ComickMuCategoryDescriptorJsonConverter : JsonConverter<ComickMuCategoryDescriptor?>
{
	/// <inheritdoc />
	public override ComickMuCategoryDescriptor? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		JsonElement root = document.RootElement;
		return root.ValueKind switch
		{
			JsonValueKind.Object => new ComickMuCategoryDescriptor
			{
				Title = ComickJsonConverterValueReader.ReadObjectStringProperty(root, "title"),
				Slug = ComickJsonConverterValueReader.ReadObjectStringProperty(root, "slug")
			},
			JsonValueKind.String => new ComickMuCategoryDescriptor
			{
				Title = root.GetString()
			},
			_ => null
		};
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, ComickMuCategoryDescriptor? value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStartObject();
		if (!string.IsNullOrWhiteSpace(value.Title))
		{
			writer.WriteString("title", value.Title);
		}

		if (!string.IsNullOrWhiteSpace(value.Slug))
		{
			writer.WriteString("slug", value.Slug);
		}

		writer.WriteEndObject();
	}
}

/// <summary>
/// Provides tolerant conversion for MangaUpdates category vote entries.
/// </summary>
internal sealed class ComickMuComicCategoryVoteJsonConverter : JsonConverter<ComickMuComicCategoryVote>
{
	/// <inheritdoc />
	public override ComickMuComicCategoryVote Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		JsonElement root = document.RootElement;
		if (root.ValueKind != JsonValueKind.Object)
		{
			throw new JsonException("Category vote value must be an object.");
		}

		ComickMuCategoryDescriptor? category = null;
		if (root.TryGetProperty("mu_categories", out JsonElement categoryElement))
		{
			category = categoryElement.ValueKind switch
			{
				JsonValueKind.Object => new ComickMuCategoryDescriptor
				{
					Title = ComickJsonConverterValueReader.ReadObjectStringProperty(categoryElement, "title"),
					Slug = ComickJsonConverterValueReader.ReadObjectStringProperty(categoryElement, "slug")
				},
				JsonValueKind.String => new ComickMuCategoryDescriptor
				{
					Title = categoryElement.GetString()
				},
				_ => null
			};
		}

		int? positiveVote = ComickJsonConverterValueReader.ReadObjectNullableInt32Property(root, "positive_vote");
		int? negativeVote = ComickJsonConverterValueReader.ReadObjectNullableInt32Property(root, "negative_vote");
		if ((category is null || string.IsNullOrWhiteSpace(category.Title)) &&
			!positiveVote.HasValue &&
			!negativeVote.HasValue)
		{
			throw new JsonException("Category vote object did not contain usable fields.");
		}

		return new ComickMuComicCategoryVote
		{
			Category = category,
			PositiveVote = positiveVote,
			NegativeVote = negativeVote
		};
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, ComickMuComicCategoryVote value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(value);

		writer.WriteStartObject();
		if (value.Category is not null)
		{
			writer.WritePropertyName("mu_categories");
			JsonSerializer.Serialize(writer, value.Category, options);
		}

		if (value.PositiveVote.HasValue)
		{
			writer.WriteNumber("positive_vote", value.PositiveVote.Value);
		}

		if (value.NegativeVote.HasValue)
		{
			writer.WriteNumber("negative_vote", value.NegativeVote.Value);
		}

		writer.WriteEndObject();
	}
}
