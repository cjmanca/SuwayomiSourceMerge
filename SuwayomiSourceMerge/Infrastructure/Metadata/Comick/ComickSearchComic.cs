using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Represents one typed Comick search result item.
/// </summary>
internal sealed class ComickSearchComic
{
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

	/// <summary>
	/// Gets or sets alternate titles returned by the search payload.
	/// Search payload aliases can be incomplete, so callers should use comic-details payloads for full alias coverage.
	/// </summary>
	[JsonPropertyName("md_titles")]
	[JsonConverter(typeof(ComickTitleAliasListConverter))]
	public IReadOnlyList<ComickTitleAlias>? MdTitles
	{
		get;
		init;
	}

	/// <summary>
	/// Gets or sets all additional search fields not required by the current matching pipeline.
	/// </summary>
	[JsonExtensionData]
	public IDictionary<string, JsonElement>? AdditionalProperties
	{
		get;
		init;
	}
}
