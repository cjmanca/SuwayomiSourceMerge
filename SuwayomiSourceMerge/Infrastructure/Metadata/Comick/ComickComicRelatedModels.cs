using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Represents external link fields from comic-detail payload.
/// </summary>
internal sealed class ComickComicLinks
{
	/// <summary>Gets or sets AniList identifier.</summary>
	[JsonPropertyName("al")]
	public string? AniList
	{
		get;
		init;
	}

	/// <summary>Gets or sets AnimePlanet slug.</summary>
	[JsonPropertyName("ap")]
	public string? AnimePlanet
	{
		get;
		init;
	}

	/// <summary>Gets or sets BookWalker token.</summary>
	[JsonPropertyName("bw")]
	public string? BookWalker
	{
		get;
		init;
	}

	/// <summary>Gets or sets Kitsu token.</summary>
	[JsonPropertyName("kt")]
	public string? Kitsu
	{
		get;
		init;
	}

	/// <summary>Gets or sets MangaBuddy identifier.</summary>
	[JsonPropertyName("mb")]
	public int? MangaBuddy
	{
		get;
		init;
	}

	/// <summary>Gets or sets MangaUpdates token.</summary>
	[JsonPropertyName("mu")]
	public string? MangaUpdates
	{
		get;
		init;
	}

	/// <summary>Gets or sets Amazon URL.</summary>
	[JsonPropertyName("amz")]
	public string? Amazon
	{
		get;
		init;
	}

	/// <summary>Gets or sets CDJapan URL.</summary>
	[JsonPropertyName("cdj")]
	public string? CdJapan
	{
		get;
		init;
	}

	/// <summary>Gets or sets EbookJapan URL.</summary>
	[JsonPropertyName("ebj")]
	public string? EbookJapan
	{
		get;
		init;
	}

	/// <summary>Gets or sets MyAnimeList identifier.</summary>
	[JsonPropertyName("mal")]
	public string? MyAnimeList
	{
		get;
		init;
	}

	/// <summary>Gets or sets raw source URL.</summary>
	[JsonPropertyName("raw")]
	public string? RawSource
	{
		get;
		init;
	}

	/// <summary>Gets or sets English translation URL.</summary>
	[JsonPropertyName("engtl")]
	public string? EnglishTranslation
	{
		get;
		init;
	}

	/// <summary>Gets or sets unknown link properties retained for compatibility.</summary>
	[JsonExtensionData]
	public IDictionary<string, JsonElement>? AdditionalProperties
	{
		get;
		init;
	}
}

/// <summary>
/// Represents one comic recommendation entry.
/// </summary>
internal sealed class ComickComicRecommendation
{
	/// <summary>Gets or sets up-vote count.</summary>
	[JsonPropertyName("up")]
	public int UpVotes
	{
		get;
		init;
	}

	/// <summary>Gets or sets down-vote count.</summary>
	[JsonPropertyName("down")]
	public int DownVotes
	{
		get;
		init;
	}

	/// <summary>Gets or sets total score value.</summary>
	[JsonPropertyName("total")]
	public int Total
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
	public string Title
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets slug text.</summary>
	[JsonPropertyName("slug")]
	public string Slug
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets hid identifier.</summary>
	[JsonPropertyName("hid")]
	public string Hid
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets cover list.</summary>
	[JsonPropertyName("md_covers")]
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
	public string Slug
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets target title.</summary>
	[JsonPropertyName("title")]
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
	public string Name
	{
		get;
		init;
	} = string.Empty;
}

/// <summary>
/// Represents one comic-genre mapping entry.
/// </summary>
internal sealed class ComickComicGenreMapping
{
	/// <summary>Gets or sets mapped genre payload.</summary>
	[JsonPropertyName("md_genres")]
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
	public string Name
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets genre type token.</summary>
	[JsonPropertyName("type")]
	public string? Type
	{
		get;
		init;
	}

	/// <summary>Gets or sets genre slug.</summary>
	[JsonPropertyName("slug")]
	public string Slug
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets genre group token.</summary>
	[JsonPropertyName("group")]
	public string? Group
	{
		get;
		init;
	}
}
