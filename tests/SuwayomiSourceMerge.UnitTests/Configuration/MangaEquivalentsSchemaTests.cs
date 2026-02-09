namespace SuwayomiSourceMerge.UnitTests.Configuration;

using SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Tests manga equivalents schema parsing and validation rules.
/// </summary>
public sealed class MangaEquivalentsSchemaTests
{
    [Fact]
    public void ParseMangaEquivalents_ShouldPassForValidDocument()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.MangaEquivalentsDocument> parsed =
            service.ParseMangaEquivalents(
                "manga_equivalents.yml",
                """
                groups:
                  - canonical: Manga Title 1
                    aliases:
                      - Manga Title One
                      - The Manga Title 1
                  - canonical: Another Manga
                    aliases: []
                """);

        Assert.True(parsed.Validation.IsValid);
        Assert.NotNull(parsed.Document);
    }

    [Fact]
    public void ParseMangaEquivalents_ShouldAllowEmptyAliasesList()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.MangaEquivalentsDocument> parsed =
            service.ParseMangaEquivalents(
                "manga_equivalents.yml",
                """
                groups:
                  - canonical: Solo Canonical
                    aliases: []
                """);

        Assert.True(parsed.Validation.IsValid);
    }

    [Fact]
    public void ParseMangaEquivalents_ShouldFailForConflictingAliasMappings()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.MangaEquivalentsDocument> parsed =
            service.ParseMangaEquivalents(
                "manga_equivalents.yml",
                """
                groups:
                  - canonical: Manga Alpha
                    aliases:
                      - Shared Alias
                  - canonical: Manga Beta
                    aliases:
                      - Shared Alias
                """);

        Assert.Contains(parsed.Validation.Errors, x => x.Code == "CFG-MEQ-005");
    }
}
