using System.Globalization;
using System.Text;

namespace SuwayomiSourceMerge.Configuration.Validation;

internal static class ValidationKeyNormalizer
{
    private static readonly string[] LeadingArticles =
    [
        "a",
        "an",
        "the"
    ];

    public static string NormalizeTitleKey(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        string folded = FoldToAscii(input).ToLowerInvariant();
        folded = ReplacePunctuationWithSpace(folded);

        string[] words = folded
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        if (words.Length == 0)
        {
            return string.Empty;
        }

        if (LeadingArticles.Contains(words[0], StringComparer.Ordinal))
        {
            words = words.Skip(1).ToArray();
        }

        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 1 && words[i].EndsWith('s'))
            {
                words[i] = words[i][..^1];
            }
        }

        return string.Concat(words);
    }

    public static string NormalizeTokenKey(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        string folded = FoldToAscii(input).ToLowerInvariant();
        folded = ReplacePunctuationWithSpace(folded);

        string[] words = folded
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        return string.Join(' ', words);
    }

    private static string ReplacePunctuationWithSpace(string input)
    {
        StringBuilder builder = new(input.Length);
        foreach (char ch in input)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append(' ');
            }
        }

        return builder.ToString();
    }

    private static string FoldToAscii(string input)
    {
        string decomposed = input.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);

        foreach (char c in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
