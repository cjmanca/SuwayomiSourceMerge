using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Represents typed comic-detail fields returned by <c>/comic/{slug}/</c>.
/// </summary>
internal sealed class ComickComicDetails
{
	/// <summary>Gets or sets numeric comic identifier.</summary>
	[JsonPropertyName("id")]
	public int Id
	{
		get;
		init;
	}

	/// <summary>Gets or sets hid identifier.</summary>
	[JsonPropertyName("hid")]
	public string Hid
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets display title.</summary>
	[JsonPropertyName("title")]
	public string Title
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets country code.</summary>
	[JsonPropertyName("country")]
	public string? Country
	{
		get;
		init;
	}

	/// <summary>Gets or sets status code.</summary>
	[JsonPropertyName("status")]
	public int? Status
	{
		get;
		init;
	}

	/// <summary>Gets or sets external link map.</summary>
	[JsonPropertyName("links")]
	public ComickComicLinks? Links
	{
		get;
		init;
	}

	/// <summary>Gets or sets last chapter number.</summary>
	[JsonPropertyName("last_chapter")]
	public int? LastChapter
	{
		get;
		init;
	}

	/// <summary>Gets or sets chapter count.</summary>
	[JsonPropertyName("chapter_count")]
	public int? ChapterCount
	{
		get;
		init;
	}

	/// <summary>Gets or sets demographic code.</summary>
	[JsonPropertyName("demographic")]
	public int? Demographic
	{
		get;
		init;
	}

	/// <summary>Gets or sets follow rank.</summary>
	[JsonPropertyName("follow_rank")]
	public int? FollowRank
	{
		get;
		init;
	}

	/// <summary>Gets or sets user-follow count.</summary>
	[JsonPropertyName("user_follow_count")]
	public int? UserFollowCount
	{
		get;
		init;
	}

	/// <summary>Gets or sets markdown description text.</summary>
	[JsonPropertyName("desc")]
	public string? Description
	{
		get;
		init;
	}

	/// <summary>Gets or sets parsed HTML description text.</summary>
	[JsonPropertyName("parsed")]
	public string? ParsedDescription
	{
		get;
		init;
	}

	/// <summary>Gets or sets comic slug.</summary>
	[JsonPropertyName("slug")]
	public string Slug
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets mismatch payload for unknown shape retention.</summary>
	[JsonPropertyName("mismatch")]
	public JsonElement? Mismatch
	{
		get;
		init;
	}

	/// <summary>Gets or sets release year.</summary>
	[JsonPropertyName("year")]
	public int? Year
	{
		get;
		init;
	}

	/// <summary>Gets or sets bayesian rating text.</summary>
	[JsonPropertyName("bayesian_rating")]
	public string? BayesianRating
	{
		get;
		init;
	}

	/// <summary>Gets or sets rating-count value.</summary>
	[JsonPropertyName("rating_count")]
	public int? RatingCount
	{
		get;
		init;
	}

	/// <summary>Gets or sets content-rating token.</summary>
	[JsonPropertyName("content_rating")]
	public string? ContentRating
	{
		get;
		init;
	}

	/// <summary>Gets or sets statistics list.</summary>
	[JsonPropertyName("statistics")]
	public IReadOnlyList<ComickStatistic>? Statistics
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether translation is complete.</summary>
	[JsonPropertyName("translation_completed")]
	public bool TranslationCompleted
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether chapter numbers reset on new volume.</summary>
	[JsonPropertyName("chapter_numbers_reset_on_new_volume_manual")]
	public bool ChapterNumbersResetOnNewVolumeManual
	{
		get;
		init;
	}

	/// <summary>Gets or sets final-chapter payload for unknown shape retention.</summary>
	[JsonPropertyName("final_chapter")]
	public JsonElement? FinalChapter
	{
		get;
		init;
	}

	/// <summary>Gets or sets final-volume payload for unknown shape retention.</summary>
	[JsonPropertyName("final_volume")]
	public JsonElement? FinalVolume
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether indexing is disabled.</summary>
	[JsonPropertyName("noindex")]
	public bool NoIndex
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether adsense is enabled.</summary>
	[JsonPropertyName("adsense")]
	public bool AdSense
	{
		get;
		init;
	}

	/// <summary>Gets or sets comment count.</summary>
	[JsonPropertyName("comment_count")]
	public int? CommentCount
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether login is required.</summary>
	[JsonPropertyName("login_required")]
	public bool LoginRequired
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether anime metadata exists.</summary>
	[JsonPropertyName("has_anime")]
	public bool HasAnime
	{
		get;
		init;
	}

	/// <summary>Gets or sets anime range metadata.</summary>
	[JsonPropertyName("anime")]
	public ComickComicAnime? Anime
	{
		get;
		init;
	}

	/// <summary>Gets or sets review entries.</summary>
	[JsonPropertyName("reviews")]
	public IReadOnlyList<ComickComicReview> Reviews
	{
		get;
		init;
	} = [];

	/// <summary>Gets or sets recommendation entries.</summary>
	[JsonPropertyName("recommendations")]
	public IReadOnlyList<ComickComicRecommendation>? Recommendations
	{
		get;
		init;
	}

	/// <summary>Gets or sets reverse-relation entries.</summary>
	[JsonPropertyName("relate_from")]
	public IReadOnlyList<ComickComicRelateFromEntry>? RelateFrom
	{
		get;
		init;
	}

	/// <summary>Gets or sets alternate title entries.</summary>
	[JsonPropertyName("md_titles")]
	public IReadOnlyList<ComickTitleAlias>? MdTitles
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether title is already English.</summary>
	[JsonPropertyName("is_english_title")]
	public bool? IsEnglishTitle
	{
		get;
		init;
	}

	/// <summary>Gets or sets genre-mapping entries.</summary>
	[JsonPropertyName("md_comic_md_genres")]
	public IReadOnlyList<ComickComicGenreMapping>? GenreMappings
	{
		get;
		init;
	}

	/// <summary>Gets or sets cover entries.</summary>
	[JsonPropertyName("md_covers")]
	public IReadOnlyList<ComickCover>? MdCovers
	{
		get;
		init;
	}

	/// <summary>Gets or sets optional MangaUpdates metadata.</summary>
	[JsonPropertyName("mu_comics")]
	public ComickMuComics? MuComics
	{
		get;
		init;
	}

	/// <summary>Gets or sets primary language code.</summary>
	[JsonPropertyName("iso639_1")]
	public string? Iso6391
	{
		get;
		init;
	}

	/// <summary>Gets or sets language display name.</summary>
	[JsonPropertyName("lang_name")]
	public string? LanguageName
	{
		get;
		init;
	}

	/// <summary>Gets or sets language native display name.</summary>
	[JsonPropertyName("lang_native")]
	public string? LanguageNative
	{
		get;
		init;
	}
}

/// <summary>
/// Represents anime start/end range metadata.
/// </summary>
internal sealed class ComickComicAnime
{
	/// <summary>Gets or sets range start text.</summary>
	[JsonPropertyName("start")]
	public string? Start
	{
		get;
		init;
	}

	/// <summary>Gets or sets range end text.</summary>
	[JsonPropertyName("end")]
	public string? End
	{
		get;
		init;
	}
}

/// <summary>
/// Represents one review object with extension-data retention for upstream schema drift.
/// </summary>
internal sealed class ComickComicReview
{
	/// <summary>
	/// Gets or sets unknown review properties.
	/// </summary>
	[JsonExtensionData]
	public IDictionary<string, JsonElement>? AdditionalProperties
	{
		get;
		init;
	}
}
