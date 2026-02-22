using System.Text.Json.Serialization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Represents typed Comick comic endpoint payload.
/// </summary>
internal sealed class ComickComicResponse
{
	/// <summary>Gets or sets first-chapter metadata.</summary>
	[JsonPropertyName("firstChap")]
	public ComickComicFirstChapter? FirstChapter
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
	public IReadOnlyList<ComickCreator> Artists
	{
		get;
		init;
	} = [];

	/// <summary>Gets or sets author entries.</summary>
	[JsonPropertyName("authors")]
	public IReadOnlyList<ComickCreator> Authors
	{
		get;
		init;
	} = [];

	/// <summary>Gets or sets language-list values.</summary>
	[JsonPropertyName("langList")]
	public IReadOnlyList<string> LanguageList
	{
		get;
		init;
	} = [];

	/// <summary>Gets or sets a value indicating whether recommendation is enabled.</summary>
	[JsonPropertyName("recommendable")]
	public bool Recommendable
	{
		get;
		init;
	}

	/// <summary>Gets or sets top-level demographic token.</summary>
	[JsonPropertyName("demographic")]
	public string? Demographic
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether content is mature.</summary>
	[JsonPropertyName("matureContent")]
	public bool MatureContent
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether volume/chapter check is enabled.</summary>
	[JsonPropertyName("checkVol2Chap1")]
	public bool CheckVol2Chap1
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
internal sealed class ComickCreator
{
	/// <summary>Gets or sets creator name.</summary>
	[JsonPropertyName("name")]
	public string Name
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets creator slug.</summary>
	[JsonPropertyName("slug")]
	public string Slug
	{
		get;
		init;
	} = string.Empty;
}
