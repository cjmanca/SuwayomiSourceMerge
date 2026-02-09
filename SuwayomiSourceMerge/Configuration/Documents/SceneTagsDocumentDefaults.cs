namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Produces default scene tag values.
/// </summary>
public static class SceneTagsDocumentDefaults
{
    /// <summary>
    /// Creates a default scene tag document based on documented tags.
    /// </summary>
    /// <returns>A scene tag document with default tags.</returns>
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
