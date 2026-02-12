namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Produces default scene tag values.
/// </summary>
/// <remarks>
/// This list mirrors the documented baseline tags and is used when bootstrap needs to create
/// <c>scene_tags.yml</c> for first-run scenarios.
/// </remarks>
public static class SceneTagsDocumentDefaults
{
	/// <summary>
	/// Creates a default scene tag document based on documented tags.
	/// </summary>
	/// <returns>A scene tag document with initialized default tags in deterministic order.</returns>
	public static SceneTagsDocument Create()
	{
		return new SceneTagsDocument
		{
			Tags =
			[
				"official",
				"color",
				"colour",
				"colorized",
				"colourized",
				"colored",
				"coloured",
				"uncensored",
				"censored",
				"asura scan",
				"asura scans",
				"team argo",
				"tapas official",
				"valir scans",
				"digital",
				"webtoon",
				"web",
				"scan",
				"scans",
				"scanlation",
				"hq",
				"hd",
				"raw",
				"raws",
				"manga",
				"manhwa",
				"manhua",
				"comic",
				"translated",
				"translation",
				"english",
				"eng",
				"en",
				"jpn",
				"jp",
				"kor",
				"kr",
				"chi",
				"cn",
				"fr",
				"es",
				"de",
				"pt",
				"it",
				"ru"
			]
		};
	}
}
