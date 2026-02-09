namespace SuwayomiSourceMerge.UnitTests.Configuration;

using FsCheck;
using FsCheck.Xunit;
using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Loading;

public sealed class YamlDocumentParserPropertyTests
{
    [Property(MaxTest = 120)]
    public bool Parse_ShouldIgnoreUnknownTopLevelField_AndPreserveKnownTags(
        NonEmptyArray<NonEmptyString> tags,
        NonNull<string> unknownValue)
    {
        YamlDocumentParser parser = new();
        List<string> sanitizedTags = tags.Get
            .Select(item => SanitizeYamlScalar(item.Get))
            .ToList();
        string yaml = BuildYaml(sanitizedTags, SanitizeYamlScalar(unknownValue.Get));

        ParsedDocument<SceneTagsDocument> parsed = parser.Parse<SceneTagsDocument>("scene_tags.yml", yaml);

        return parsed.Validation.IsValid
            && parsed.Document is not null
            && parsed.Document.Tags is not null
            && parsed.Document.Tags.SequenceEqual(sanitizedTags);
    }

    private static string BuildYaml(IEnumerable<string> tags, string unknownValue)
    {
        List<string> lines = ["tags:"];
        foreach (string tag in tags)
        {
            lines.Add($"  - '{EscapeSingleQuoted(tag)}'");
        }

        lines.Add($"unknown_field: '{EscapeSingleQuoted(unknownValue)}'");
        return string.Join('\n', lines);
    }

    private static string EscapeSingleQuoted(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string SanitizeYamlScalar(string value)
    {
        char[] buffer = value
            .Select(character => char.IsControl(character) ? ' ' : character)
            .ToArray();
        string singleLine = new string(buffer)
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Trim();
        return string.IsNullOrEmpty(singleLine) ? "x" : singleLine;
    }
}
