using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Represents typed Comick comic endpoint payload.
/// </summary>
internal sealed class ComickComicResponse
{
	/// <summary>Gets or sets first-chapter metadata.</summary>
	[JsonPropertyName("firstChap")]
	public JsonElement? FirstChapter
	{
		get;
		init;
	}

	/// <summary>Gets or sets comic detail payload.</summary>
	[JsonPropertyName("comic")]
	public ComickComicDetails? Comic
	{
		get;
		init;
	}

	/// <summary>Gets or sets artist entries.</summary>
	[JsonPropertyName("artists")]
	[JsonConverter(typeof(ComickFilteredListJsonConverter<ComickCreator>))]
	public IReadOnlyList<ComickCreator> Artists
	{
		get;
		init;
	} = [];

	/// <summary>Gets or sets author entries.</summary>
	[JsonPropertyName("authors")]
	[JsonConverter(typeof(ComickFilteredListJsonConverter<ComickCreator>))]
	public IReadOnlyList<ComickCreator> Authors
	{
		get;
		init;
	} = [];

	/// <summary>Gets or sets language-list values.</summary>
	[JsonPropertyName("langList")]
	public JsonElement? LanguageList
	{
		get;
		init;
	}

	/// <summary>Gets or sets recommendation-enabled payload in raw form.</summary>
	[JsonPropertyName("recommendable")]
	public JsonElement? Recommendable
	{
		get;
		init;
	}

	/// <summary>Gets or sets top-level demographic payload in raw form.</summary>
	[JsonPropertyName("demographic")]
	public JsonElement? Demographic
	{
		get;
		init;
	}

	/// <summary>Gets or sets mature-content payload in raw form.</summary>
	[JsonPropertyName("matureContent")]
	public JsonElement? MatureContent
	{
		get;
		init;
	}

	/// <summary>Gets or sets volume/chapter-check payload in raw form.</summary>
	[JsonPropertyName("checkVol2Chap1")]
	public JsonElement? CheckVol2Chap1
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets unknown top-level payload properties.
	/// </summary>
	[JsonExtensionData]
	public IDictionary<string, JsonElement>? AdditionalProperties
	{
		get;
		init;
	}
}

/// <summary>
/// Represents first-chapter metadata from the comic endpoint.
/// </summary>
internal sealed class ComickComicFirstChapter
{
	/// <summary>Gets or sets chapter token.</summary>
	[JsonPropertyName("chap")]
	public string? Chapter
	{
		get;
		init;
	}

	/// <summary>Gets or sets chapter hid token.</summary>
	[JsonPropertyName("hid")]
	public string Hid
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets language code.</summary>
	[JsonPropertyName("lang")]
	public string? Language
	{
		get;
		init;
	}

	/// <summary>Gets or sets group-name values.</summary>
	[JsonPropertyName("group_name")]
	public IReadOnlyList<string> GroupNames
	{
		get;
		init;
	} = [];

	/// <summary>Gets or sets volume token.</summary>
	[JsonPropertyName("vol")]
	public string? Volume
	{
		get;
		init;
	}
}

/// <summary>
/// Represents one creator entry in comic author/artist lists.
/// </summary>
[JsonConverter(typeof(ComickCreatorJsonConverter))]
internal sealed class ComickCreator
{
	/// <summary>Gets or sets creator name.</summary>
	[JsonPropertyName("name")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Name
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets creator slug.</summary>
	[JsonPropertyName("slug")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Slug
	{
		get;
		init;
	} = string.Empty;
}
