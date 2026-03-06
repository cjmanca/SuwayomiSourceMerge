using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Represents one Comick score statistics entry.
/// </summary>
internal sealed class ComickStatistic
{
	/// <summary>
	/// Gets or sets the score-count value.
	/// </summary>
	[JsonPropertyName("score_count")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? ScoreCount
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets the weighted-score text.
	/// </summary>
	[JsonPropertyName("weighted_score")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string WeightedScore
	{
		get;
		init;
	} = string.Empty;

	/// <summary>
	/// Gets or sets score distribution values keyed from 1..10.
	/// </summary>
	[JsonPropertyName("distribution")]
	[JsonConverter(typeof(ComickOptionalObjectJsonConverter<ComickScoreDistribution>))]
	public ComickScoreDistribution? Distribution
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets score text.
	/// </summary>
	[JsonPropertyName("score")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Score
	{
		get;
		init;
	} = string.Empty;
}

/// <summary>
/// Represents one Comick score-distribution value map for vote buckets 1..10.
/// </summary>
internal sealed class ComickScoreDistribution
{
	/// <summary>Gets or sets votes for score bucket 1.</summary>
	[JsonPropertyName("1")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? One
	{
		get;
		init;
	}

	/// <summary>Gets or sets votes for score bucket 2.</summary>
	[JsonPropertyName("2")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Two
	{
		get;
		init;
	}

	/// <summary>Gets or sets votes for score bucket 3.</summary>
	[JsonPropertyName("3")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Three
	{
		get;
		init;
	}

	/// <summary>Gets or sets votes for score bucket 4.</summary>
	[JsonPropertyName("4")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Four
	{
		get;
		init;
	}

	/// <summary>Gets or sets votes for score bucket 5.</summary>
	[JsonPropertyName("5")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Five
	{
		get;
		init;
	}

	/// <summary>Gets or sets votes for score bucket 6.</summary>
	[JsonPropertyName("6")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Six
	{
		get;
		init;
	}

	/// <summary>Gets or sets votes for score bucket 7.</summary>
	[JsonPropertyName("7")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Seven
	{
		get;
		init;
	}

	/// <summary>Gets or sets votes for score bucket 8.</summary>
	[JsonPropertyName("8")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Eight
	{
		get;
		init;
	}

	/// <summary>Gets or sets votes for score bucket 9.</summary>
	[JsonPropertyName("9")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Nine
	{
		get;
		init;
	}

	/// <summary>Gets or sets votes for score bucket 10.</summary>
	[JsonPropertyName("10")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Ten
	{
		get;
		init;
	}
}

/// <summary>
/// Represents one Comick title alias entry.
/// </summary>
internal sealed class ComickTitleAlias
{
	/// <summary>
	/// Gets or sets title text.
	/// </summary>
	[JsonPropertyName("title")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string Title
	{
		get;
		init;
	} = string.Empty;

	/// <summary>
	/// Gets or sets optional language code.
	/// </summary>
	[JsonPropertyName("lang")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? Language
	{
		get;
		init;
	}
}

/// <summary>
/// Represents one Comick cover entry.
/// </summary>
[JsonConverter(typeof(ComickCoverJsonConverter))]
internal sealed class ComickCover
{
	/// <summary>
	/// Gets or sets optional volume token.
	/// </summary>
	[JsonPropertyName("vol")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? Volume
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets image width.
	/// </summary>
	[JsonPropertyName("w")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Width
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets image height.
	/// </summary>
	[JsonPropertyName("h")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Height
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets B2 storage key.
	/// </summary>
	[JsonPropertyName("b2key")]
	[JsonConverter(typeof(ComickTolerantStringConverter))]
	public string B2Key
	{
		get;
		init;
	} = string.Empty;
}

/// <summary>
/// Represents optional MangaUpdates-derived metadata.
/// </summary>
internal sealed class ComickMuComics
{
	/// <summary>
	/// Gets or sets release year.
	/// </summary>
	[JsonPropertyName("year")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? Year
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets MangaUpdates category vote entries.
	/// </summary>
	[JsonPropertyName("mu_comic_categories")]
	[JsonConverter(typeof(ComickFilteredListJsonConverter<ComickMuComicCategoryVote>))]
	public IReadOnlyList<ComickMuComicCategoryVote>? MuComicCategories
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets additional unknown properties.
	/// </summary>
	[JsonExtensionData]
	public IDictionary<string, JsonElement>? AdditionalProperties
	{
		get;
		init;
	}
}

/// <summary>
/// Represents one MangaUpdates category vote aggregate entry.
/// </summary>
[JsonConverter(typeof(ComickMuComicCategoryVoteJsonConverter))]
internal sealed class ComickMuComicCategoryVote
{
	/// <summary>
	/// Gets or sets category descriptor metadata.
	/// </summary>
	[JsonPropertyName("mu_categories")]
	[JsonConverter(typeof(ComickMuCategoryDescriptorJsonConverter))]
	public ComickMuCategoryDescriptor? Category
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets positive vote count.
	/// </summary>
	[JsonPropertyName("positive_vote")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? PositiveVote
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets negative vote count.
	/// </summary>
	[JsonPropertyName("negative_vote")]
	[JsonConverter(typeof(ComickTolerantNullableInt32Converter))]
	public int? NegativeVote
	{
		get;
		init;
	}
}

/// <summary>
/// Represents one MangaUpdates category descriptor.
/// </summary>
internal sealed class ComickMuCategoryDescriptor
{
	/// <summary>
	/// Gets or sets category title text.
	/// </summary>
	[JsonPropertyName("title")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? Title
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets category slug text.
	/// </summary>
	[JsonPropertyName("slug")]
	[JsonConverter(typeof(ComickTolerantNullableStringConverter))]
	public string? Slug
	{
		get;
		init;
	}
}
