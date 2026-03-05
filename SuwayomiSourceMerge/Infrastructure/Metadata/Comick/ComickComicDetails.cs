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
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Id
	{
		get;
		init;
	}

	/// <summary>Gets or sets hid identifier.</summary>
	[JsonPropertyName("hid")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Hid
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets display title.</summary>
	[JsonPropertyName("title")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Title
	{
		get;
		init;
	} = string.Empty;

	/// <summary>Gets or sets country code.</summary>
	[JsonPropertyName("country")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? Country
	{
		get;
		init;
	}

	/// <summary>Gets or sets status code.</summary>
	[JsonPropertyName("status")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Status
	{
		get;
		init;
	}

	/// <summary>Gets or sets external link map.</summary>
	[JsonPropertyName("links")]
	[JsonConverter(typeof(ComickOptionalObjectJsonConverter<ComickComicLinks>))]
	public ComickComicLinks? Links
	{
		get;
		init;
	}

	/// <summary>Gets or sets last chapter number.</summary>
	[JsonPropertyName("last_chapter")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? LastChapter
	{
		get;
		init;
	}

	/// <summary>Gets or sets chapter count.</summary>
	[JsonPropertyName("chapter_count")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? ChapterCount
	{
		get;
		init;
	}

	/// <summary>Gets or sets demographic code.</summary>
	[JsonPropertyName("demographic")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Demographic
	{
		get;
		init;
	}

	/// <summary>Gets or sets follow rank.</summary>
	[JsonPropertyName("follow_rank")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? FollowRank
	{
		get;
		init;
	}

	/// <summary>Gets or sets user-follow count.</summary>
	[JsonPropertyName("user_follow_count")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? UserFollowCount
	{
		get;
		init;
	}

	/// <summary>Gets or sets markdown description text.</summary>
	[JsonPropertyName("desc")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? Description
	{
		get;
		init;
	}

	/// <summary>Gets or sets parsed HTML description text.</summary>
	[JsonPropertyName("parsed")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? ParsedDescription
	{
		get;
		init;
	}

	/// <summary>Gets or sets comic slug.</summary>
	[JsonPropertyName("slug")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
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
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Year
	{
		get;
		init;
	}

	/// <summary>Gets or sets bayesian rating text.</summary>
	[JsonPropertyName("bayesian_rating")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? BayesianRating
	{
		get;
		init;
	}

	/// <summary>Gets or sets rating-count value.</summary>
	[JsonPropertyName("rating_count")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? RatingCount
	{
		get;
		init;
	}

	/// <summary>Gets or sets content-rating token.</summary>
	[JsonPropertyName("content_rating")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? ContentRating
	{
		get;
		init;
	}

	/// <summary>Gets or sets statistics list.</summary>
	[JsonPropertyName("statistics")]
	[JsonConverter(typeof(ComickFilteredListJsonConverter<ComickStatistic>))]
	public IReadOnlyList<ComickStatistic>? Statistics
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether translation is complete.</summary>
	[JsonPropertyName("translation_completed")]
	[JsonConverter(typeof(ComickTolerantBooleanConverter))]
	public bool TranslationCompleted
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether chapter numbers reset on new volume.</summary>
	[JsonPropertyName("chapter_numbers_reset_on_new_volume_manual")]
	[JsonConverter(typeof(ComickTolerantBooleanConverter))]
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
	[JsonConverter(typeof(ComickTolerantBooleanConverter))]
	public bool NoIndex
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether adsense is enabled.</summary>
	[JsonPropertyName("adsense")]
	[JsonConverter(typeof(ComickTolerantBooleanConverter))]
	public bool AdSense
	{
		get;
		init;
	}

	/// <summary>Gets or sets comment count.</summary>
	[JsonPropertyName("comment_count")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? CommentCount
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether login is required.</summary>
	[JsonPropertyName("login_required")]
	[JsonConverter(typeof(ComickTolerantBooleanConverter))]
	public bool LoginRequired
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether anime metadata exists.</summary>
	[JsonPropertyName("has_anime")]
	[JsonConverter(typeof(ComickTolerantBooleanConverter))]
	public bool HasAnime
	{
		get;
		init;
	}

	/// <summary>Gets or sets anime range metadata.</summary>
	[JsonPropertyName("anime")]
	public JsonElement? Anime
	{
		get;
		init;
	}

	/// <summary>Gets or sets review payload in raw form.</summary>
	[JsonPropertyName("reviews")]
	public JsonElement? Reviews
	{
		get;
		init;
	}

	/// <summary>Gets or sets recommendation entries.</summary>
	[JsonPropertyName("recommendations")]
	[JsonConverter(typeof(ComickFilteredListJsonConverter<ComickComicRecommendation>))]
	public IReadOnlyList<ComickComicRecommendation>? Recommendations
	{
		get;
		init;
	}

	/// <summary>Gets or sets reverse-relation entries.</summary>
	[JsonPropertyName("relate_from")]
	[JsonConverter(typeof(ComickFilteredListJsonConverter<ComickComicRelateFromEntry>))]
	public IReadOnlyList<ComickComicRelateFromEntry>? RelateFrom
	{
		get;
		init;
	}

	/// <summary>Gets or sets alternate title entries.</summary>
	[JsonPropertyName("md_titles")]
	[JsonConverter(typeof(ComickTitleAliasListConverter))]
	public IReadOnlyList<ComickTitleAlias>? MdTitles
	{
		get;
		init;
	}

	/// <summary>Gets or sets a value indicating whether title is already English.</summary>
	[JsonPropertyName("is_english_title")]
	[JsonConverter(typeof(ComickTolerantNullableBooleanConverter))]
	public bool? IsEnglishTitle
	{
		get;
		init;
	}

	/// <summary>Gets or sets genre-mapping entries.</summary>
	[JsonPropertyName("md_comic_md_genres")]
	[JsonConverter(typeof(ComickFilteredListJsonConverter<ComickComicGenreMapping>))]
	public IReadOnlyList<ComickComicGenreMapping>? GenreMappings
	{
		get;
		init;
	}

	/// <summary>Gets or sets cover entries.</summary>
	[JsonPropertyName("md_covers")]
	[JsonConverter(typeof(ComickFilteredListJsonConverter<ComickCover>))]
	public IReadOnlyList<ComickCover>? MdCovers
	{
		get;
		init;
	}

	/// <summary>Gets or sets optional MangaUpdates metadata.</summary>
	[JsonPropertyName("mu_comics")]
	[JsonConverter(typeof(ComickOptionalObjectJsonConverter<ComickMuComics>))]
	public ComickMuComics? MuComics
	{
		get;
		init;
	}

	/// <summary>Gets or sets primary language code.</summary>
	[JsonPropertyName("iso639_1")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? Iso6391
	{
		get;
		init;
	}

	/// <summary>Gets or sets language display name.</summary>
	[JsonPropertyName("lang_name")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? LanguageName
	{
		get;
		init;
	}

	/// <summary>Gets or sets language native display name.</summary>
	[JsonPropertyName("lang_native")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? LanguageNative
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets unknown comic-node payload properties.
	/// </summary>
	[JsonExtensionData]
	public IDictionary<string, JsonElement>? AdditionalProperties
	{
		get;
		init;
	}
}
