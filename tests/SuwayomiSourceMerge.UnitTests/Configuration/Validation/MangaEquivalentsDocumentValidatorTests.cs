namespace SuwayomiSourceMerge.UnitTests.Configuration.Validation;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Validation;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.UnitTests.Configuration;

public sealed class MangaEquivalentsDocumentValidatorTests
{
    [Fact]
    public void Constructors_ShouldProduceEquivalentResults_WhenMatcherIsNull()
    {
        MangaEquivalentsDocument document = ConfigurationTestData.CreateValidMangaEquivalentsDocument();
        MangaEquivalentsDocumentValidator defaultValidator = new();
        MangaEquivalentsDocumentValidator nullMatcherValidator = new(sceneTagMatcher: null);

        ValidationResult expected = defaultValidator.Validate(document, "manga_equivalents.yml");
        ValidationResult actual = nullMatcherValidator.Validate(document, "manga_equivalents.yml");

        Assert.Equal(expected.IsValid, actual.IsValid);
        Assert.Equal(expected.Errors.Count, actual.Errors.Count);
    }

    [Fact]
    public void Validate_ShouldPass_ForValidDocument()
    {
        MangaEquivalentsDocumentValidator validator = new();

        ValidationResult result = validator.Validate(ConfigurationTestData.CreateValidMangaEquivalentsDocument(), "manga_equivalents.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldAllow_EmptyAliasList()
    {
        MangaEquivalentsDocumentValidator validator = new();
        MangaEquivalentsDocument document = new()
        {
            Groups =
            [
                new MangaEquivalentGroup
                {
                    Canonical = "Manga Alpha",
                    Aliases = []
                }
            ]
        };

        ValidationResult result = validator.Validate(document, "manga_equivalents.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldReportDeterministicError_ForConflictingAlias()
    {
        MangaEquivalentsDocumentValidator validator = new();
        MangaEquivalentsDocument document = new()
        {
            Groups =
            [
                new MangaEquivalentGroup
                {
                    Canonical = "Manga Alpha",
                    Aliases = ["Shared Alias"]
                },
                new MangaEquivalentGroup
                {
                    Canonical = "Manga Beta",
                    Aliases = ["Shared Alias"]
                }
            ]
        };

        ValidationResult result = validator.Validate(document, "manga_equivalents.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("manga_equivalents.yml", error.File);
        Assert.Equal("$.groups[1].aliases[0]", error.Path);
        Assert.Equal("CFG-MEQ-005", error.Code);
    }

    [Fact]
    public void Validate_ShouldThrow_WhenDocumentIsNull()
    {
        MangaEquivalentsDocumentValidator validator = new();

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!, "manga_equivalents.yml"));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenFileIsWhitespace()
    {
        MangaEquivalentsDocumentValidator validator = new();

        Assert.Throws<ArgumentException>(() => validator.Validate(ConfigurationTestData.CreateValidMangaEquivalentsDocument(), " "));
    }

    [Fact]
    public void Validate_ShouldReportMissingGroups_WhenGroupsListIsNull()
    {
        MangaEquivalentsDocumentValidator validator = new();
        MangaEquivalentsDocument document = new()
        {
            Groups = null
        };

        ValidationResult result = validator.Validate(document, "manga_equivalents.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("$.groups", error.Path);
        Assert.Equal("CFG-MEQ-001", error.Code);
    }

    [Fact]
    public void Validate_ShouldReportMissingCanonicalAndAliases_WhenEntriesAreIncomplete()
    {
        MangaEquivalentsDocumentValidator validator = new();
        MangaEquivalentsDocument document = new()
        {
            Groups =
            [
                new MangaEquivalentGroup
                {
                    Canonical = " ",
                    Aliases = []
                },
                new MangaEquivalentGroup
                {
                    Canonical = "Manga Alpha",
                    Aliases = null
                }
            ]
        };

        ValidationResult result = validator.Validate(document, "manga_equivalents.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.groups[0].canonical" && error.Code == "CFG-MEQ-002");
        Assert.Contains(result.Errors, error => error.Path == "$.groups[1].aliases" && error.Code == "CFG-MEQ-003");
    }

    [Fact]
    public void Validate_ShouldReportDuplicateCanonicalAndEmptyAliasAfterNormalization()
    {
        MangaEquivalentsDocumentValidator validator = new();
        MangaEquivalentsDocument document = new()
        {
            Groups =
            [
                new MangaEquivalentGroup
                {
                    Canonical = "Manga Alpha",
                    Aliases = ["Shared Alias"]
                },
                new MangaEquivalentGroup
                {
                    Canonical = "Manga-Alpha",
                    Aliases = [" ", "!!!"]
                }
            ]
        };

        ValidationResult result = validator.Validate(document, "manga_equivalents.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.groups[1].canonical" && error.Code == "CFG-MEQ-004");
        Assert.Contains(result.Errors, error => error.Path == "$.groups[1].aliases[0]" && error.Code == "CFG-MEQ-006");
        Assert.Contains(result.Errors, error => error.Path == "$.groups[1].aliases[1]" && error.Code == "CFG-MEQ-006");
    }

    [Fact]
    public void Validate_ShouldReportDuplicateCanonical_WhenSceneTagMatcherStripsTrailingSuffixes()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);
        MangaEquivalentsDocumentValidator validator = new(matcher);
        MangaEquivalentsDocument document = new()
        {
            Groups =
            [
                new MangaEquivalentGroup
                {
                    Canonical = "Manga [Official]",
                    Aliases = []
                },
                new MangaEquivalentGroup
                {
                    Canonical = "Manga",
                    Aliases = []
                }
            ]
        };

        ValidationResult result = validator.Validate(document, "manga_equivalents.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.groups[1].canonical" && error.Code == "CFG-MEQ-004");
    }

    [Fact]
    public void Validate_ShouldRemainValidWithoutSceneTagMatcher_ForSuffixVariants()
    {
        MangaEquivalentsDocumentValidator validator = new();
        MangaEquivalentsDocument document = new()
        {
            Groups =
            [
                new MangaEquivalentGroup
                {
                    Canonical = "Manga [Official]",
                    Aliases = []
                },
                new MangaEquivalentGroup
                {
                    Canonical = "Manga",
                    Aliases = []
                }
            ]
        };

        ValidationResult result = validator.Validate(document, "manga_equivalents.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldReportConflictingAlias_WhenSceneTagMatcherNormalizesAliasSuffix()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);
        MangaEquivalentsDocumentValidator validator = new(matcher);
        MangaEquivalentsDocument document = new()
        {
            Groups =
            [
                new MangaEquivalentGroup
                {
                    Canonical = "Manga One",
                    Aliases = ["Shared Alias"]
                },
                new MangaEquivalentGroup
                {
                    Canonical = "Manga Two",
                    Aliases = ["Shared Alias [Official]"]
                }
            ]
        };

        ValidationResult result = validator.Validate(document, "manga_equivalents.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.groups[1].aliases[0]" && error.Code == "CFG-MEQ-005");
    }

    [Fact]
    public void Validate_ShouldReportEmptyAlias_WhenMatcherStripsAliasToEmpty()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);
        MangaEquivalentsDocumentValidator validator = new(matcher);
        MangaEquivalentsDocument document = new()
        {
            Groups =
            [
                new MangaEquivalentGroup
                {
                    Canonical = "Manga One",
                    Aliases = ["[Official]"]
                }
            ]
        };

        ValidationResult result = validator.Validate(document, "manga_equivalents.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.groups[0].aliases[0]" && error.Code == "CFG-MEQ-006");
    }

    [Fact]
    public void Validate_ShouldNotReportConflict_WhenAliasCollidesWithinSameCanonical()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);
        MangaEquivalentsDocumentValidator validator = new(matcher);
        MangaEquivalentsDocument document = new()
        {
            Groups =
            [
                new MangaEquivalentGroup
                {
                    Canonical = "Manga One",
                    Aliases = ["Shared Alias", "Shared Alias [Official]"]
                }
            ]
        };

        ValidationResult result = validator.Validate(document, "manga_equivalents.yml");

        Assert.True(result.IsValid);
    }
}
