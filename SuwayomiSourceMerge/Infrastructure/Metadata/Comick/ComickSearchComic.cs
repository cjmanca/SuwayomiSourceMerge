using System.Text.Json.Serialization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Represents one typed Comick search result item.
/// </summary>
internal sealed class ComickSearchComic
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

	/// <summary>Gets or sets comic slug.</summary>
	[JsonPropertyName("slug")]
	public string Slug
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

	/// <summary>Gets or sets rating text.</summary>
	[JsonPropertyName("rating")]
	public string? Rating
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

	/// <summary>Gets or sets statistics entries.</summary>
	[JsonPropertyName("statistics")]
	public IReadOnlyList<ComickStatistic>? Statistics
	{
		get;
		init;
	}

	/// <summary>Gets or sets description text.</summary>
	[JsonPropertyName("desc")]
	public string? Description
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

	/// <summary>Gets or sets last-chapter number.</summary>
	[JsonPropertyName("last_chapter")]
	public int? LastChapter
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

	/// <summary>Gets or sets view-count value.</summary>
	[JsonPropertyName("view_count")]
	public int? ViewCount
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

	/// <summary>Gets or sets demographic code.</summary>
	[JsonPropertyName("demographic")]
	public int? Demographic
	{
		get;
		init;
	}

	/// <summary>Gets or sets upload timestamp.</summary>
	[JsonPropertyName("uploaded_at")]
	public DateTimeOffset? UploadedAtUtc
	{
		get;
		init;
	}

	/// <summary>Gets or sets genre identifiers.</summary>
	[JsonPropertyName("genres")]
	public IReadOnlyList<int> GenreIds
	{
		get;
		init;
	} = [];

	/// <summary>Gets or sets creation timestamp.</summary>
	[JsonPropertyName("created_at")]
	public DateTimeOffset? CreatedAtUtc
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

	/// <summary>Gets or sets release year.</summary>
	[JsonPropertyName("year")]
	public int? Year
	{
		get;
		init;
	}

	/// <summary>Gets or sets country code.</summary>
	[JsonPropertyName("country")]
	public string? Country
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

	/// <summary>Gets or sets alternate titles.</summary>
	[JsonPropertyName("md_titles")]
	public IReadOnlyList<ComickTitleAlias>? MdTitles
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

	/// <summary>Gets or sets highlighted search text.</summary>
	[JsonPropertyName("highlight")]
	public string? Highlight
	{
		get;
		init;
	}
}
