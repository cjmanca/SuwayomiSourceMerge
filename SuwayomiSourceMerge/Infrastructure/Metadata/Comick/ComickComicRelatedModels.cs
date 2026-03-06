using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Represents dynamic external link fields from comic-detail payload.
/// </summary>
internal sealed class ComickComicLinks
{
	/// <summary>
	/// Gets or sets dynamic link entries keyed by upstream link identifiers.
	/// </summary>
	[JsonExtensionData]
	public IDictionary<string, JsonElement> Entries
	{
		get;
		init;
	} = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

	/// <summary>
	/// Attempts to read one link entry by key.
	/// </summary>
	/// <param name="key">Link key.</param>
	/// <param name="value">Link value when found.</param>
	/// <returns><see langword="true"/> when key exists; otherwise <see langword="false"/>.</returns>
	public bool TryGetEntry(string key, out JsonElement value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		return Entries.TryGetValue(key, out value);
	}
}

/// <summary>
/// Represents one comic recommendation entry.
/// </summary>
internal sealed class ComickComicRecommendation
{
	/// <summary>Gets or sets up-vote count.</summary>
	[JsonPropertyName("up")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? UpVotes
	{
		get;
		init;
	}

	/// <summary>Gets or sets down-vote count.</summary>
	[JsonPropertyName("down")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? DownVotes
	{
		get;
		init;
	}

	/// <summary>Gets or sets total score value.</summary>
	[JsonPropertyName("total")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Total
	{
		get;
		init;
	}

	/// <summary>Gets or sets related-comic payload.</summary>
	[JsonPropertyName("relates")]
	public ComickComicRecommendationRelates? Relates
	{
		get;
		init;
	}
}

/// <summary>
/// Represents recommendation related-comic payload.
/// </summary>
internal sealed class ComickComicRecommendationRelates
{
	/// <summary>Gets or sets title text.</summary>
	[JsonPropertyName("title")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Title
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets slug text.</summary>
	[JsonPropertyName("slug")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Slug
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets hid identifier.</summary>
	[JsonPropertyName("hid")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Hid
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets cover list.</summary>
	[JsonPropertyName("md_covers")]
	[JsonConverter(typeof(ComickFilteredListJsonConverter<ComickCover>))]
	public IReadOnlyList<ComickCover> MdCovers
	{
		get;
		init;
	} = [];
}

/// <summary>
/// Represents one reverse-relation entry.
/// </summary>
internal sealed class ComickComicRelateFromEntry
{
	/// <summary>Gets or sets relation target payload.</summary>
	[JsonPropertyName("relate_to")]
	public ComickComicRelateTarget? RelateTo
	{
		get;
		init;
	}

	/// <summary>Gets or sets relation metadata payload.</summary>
	[JsonPropertyName("md_relates")]
	public ComickComicRelation? Relation
	{
		get;
		init;
	}
}

/// <summary>
/// Represents one relation target.
/// </summary>
internal sealed class ComickComicRelateTarget
{
	/// <summary>Gets or sets target slug.</summary>
	[JsonPropertyName("slug")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Slug
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets target title.</summary>
	[JsonPropertyName("title")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Title
	{
		get;
		init;
	} = string.Empty;
}

/// <summary>
/// Represents one relation metadata object.
/// </summary>
internal sealed class ComickComicRelation
{
	/// <summary>Gets or sets relation name.</summary>
	[JsonPropertyName("name")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Name
	{
		get;
		init;
	} = string.Empty;
}

/// <summary>
/// Represents one comic-genre mapping entry.
/// </summary>
[JsonConverter(typeof(ComickComicGenreMappingJsonConverter))]
internal sealed class ComickComicGenreMapping
{
	/// <summary>Gets or sets mapped genre payload.</summary>
	[JsonPropertyName("md_genres")]
	[JsonConverter(typeof(ComickOptionalObjectJsonConverter<ComickGenreDescriptor>))]
	public ComickGenreDescriptor? Genre
	{
		get;
		init;
	}
}

/// <summary>
/// Represents one genre descriptor.
/// </summary>
internal sealed class ComickGenreDescriptor
{
	/// <summary>Gets or sets genre name.</summary>
	[JsonPropertyName("name")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Name
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets genre type token.</summary>
	[JsonPropertyName("type")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? Type
	{
		get;
		init;
	}

	/// <summary>Gets or sets genre slug.</summary>
	[JsonPropertyName("slug")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Slug
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets genre group token.</summary>
	[JsonPropertyName("group")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? Group
	{
		get;
		init;
	}
}
