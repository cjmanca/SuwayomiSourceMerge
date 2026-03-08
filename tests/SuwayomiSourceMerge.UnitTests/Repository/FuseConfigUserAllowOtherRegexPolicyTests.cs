namespace SuwayomiSourceMerge.UnitTests.Repository;

using System.Text.RegularExpressions;

using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies the canonical <c>user_allow_other</c> regex literal remains consistent across docs, scripts, and tests.
/// </summary>
public sealed class FuseConfigUserAllowOtherRegexPolicyTests
{
	/// <summary>
	/// Canonical regex used to detect a configured <c>user_allow_other</c> entry.
	/// </summary>
	private const string CanonicalUserAllowOtherRegex = "^[[:space:]]*user_allow_other([[:space:]]*#.*)?[[:space:]]*$";

	/// <summary>
	/// Canonical files that embed the grep expression for <c>user_allow_other</c> handling.
	/// </summary>
	private static readonly string[] _canonicalRegexFiles =
	[
		"docker/entrypoint.sh",
		"tools/setup-host-security.sh",
		"README.md",
		"tests/SuwayomiSourceMerge.IntegrationTests/ContainerRuntimeEndToEndTests.cs"
	];

	/// <summary>
	/// Ensures all canonical <c>user_allow_other</c> grep regex literals stay in sync.
	/// </summary>
	[Fact]
	public void UserAllowOtherRegexLiterals_ShouldMatchCanonicalPatternAcrossCanonicalFiles()
	{
		string repositoryRoot = RepositoryRootLocator.FindRepositoryRoot();
		IReadOnlyList<RegexLiteralOccurrence> occurrences = FindCanonicalRegexLiterals(repositoryRoot);

		Assert.NotEmpty(occurrences);

		List<RegexLiteralOccurrence> mismatches = occurrences
			.Where(static occurrence => !StringComparer.Ordinal.Equals(occurrence.Pattern, CanonicalUserAllowOtherRegex))
			.ToList();

		Assert.True(mismatches.Count == 0, BuildMismatchFailureMessage(mismatches));
	}

	/// <summary>
	/// Reads canonical files and extracts grep regex literals containing <c>user_allow_other</c>.
	/// </summary>
	/// <param name="repositoryRoot">Absolute repository root path.</param>
	/// <returns>Extracted regex literal occurrences.</returns>
	private static IReadOnlyList<RegexLiteralOccurrence> FindCanonicalRegexLiterals(string repositoryRoot)
	{
		Regex grepRegexLiteralPattern = new("grep -Eq '(?<pattern>[^']+)'", RegexOptions.CultureInvariant);
		List<RegexLiteralOccurrence> occurrences = [];

		foreach (string relativeFilePath in _canonicalRegexFiles)
		{
			string absolutePath = Path.Combine(repositoryRoot, relativeFilePath.Replace('/', Path.DirectorySeparatorChar));
			string fileContent = File.ReadAllText(absolutePath);
			MatchCollection matches = grepRegexLiteralPattern.Matches(fileContent);

			List<RegexLiteralOccurrence> fileOccurrences = [];
			foreach (Match match in matches)
			{
				string pattern = match.Groups["pattern"].Value;
				if (!pattern.Contains("user_allow_other", StringComparison.Ordinal))
				{
					continue;
				}

				int lineNumber = CalculateLineNumber(fileContent, match.Index);
				fileOccurrences.Add(new RegexLiteralOccurrence(relativeFilePath, lineNumber, pattern));
			}

			Assert.True(
				fileOccurrences.Count > 0,
				$"Expected at least one user_allow_other regex literal in '{relativeFilePath}', but none were found.");

			occurrences.AddRange(fileOccurrences);
		}

		return occurrences;
	}

	/// <summary>
	/// Converts a character index into a one-based line number.
	/// </summary>
	/// <param name="content">Source text content.</param>
	/// <param name="characterIndex">Zero-based character index into <paramref name="content"/>.</param>
	/// <returns>One-based line number for the index.</returns>
	private static int CalculateLineNumber(string content, int characterIndex)
	{
		int lineNumber = 1;
		for (int index = 0; index < characterIndex && index < content.Length; index++)
		{
			if (content[index] == '\n')
			{
				lineNumber++;
			}
		}

		return lineNumber;
	}

	/// <summary>
	/// Builds deterministic assertion output for regex literal mismatches.
	/// </summary>
	/// <param name="mismatches">Mismatching regex occurrences.</param>
	/// <returns>Assertion message.</returns>
	private static string BuildMismatchFailureMessage(IReadOnlyList<RegexLiteralOccurrence> mismatches)
	{
		if (mismatches.Count == 0)
		{
			return string.Empty;
		}

		List<string> lines =
		[
			"Detected user_allow_other regex literal drift.",
			$"Canonical regex: {CanonicalUserAllowOtherRegex}",
			"Mismatches:"
		];

		foreach (RegexLiteralOccurrence mismatch in mismatches)
		{
			lines.Add($" - {mismatch.RelativeFilePath}:{mismatch.LineNumber} => {mismatch.Pattern}");
		}

		return string.Join(Environment.NewLine, lines);
	}

	/// <summary>
	/// Captures one regex literal occurrence found in a tracked canonical file.
	/// </summary>
	/// <param name="RelativeFilePath">Workspace-relative file path.</param>
	/// <param name="LineNumber">One-based line number of the literal occurrence.</param>
	/// <param name="Pattern">Regex pattern extracted from the grep expression.</param>
	private readonly record struct RegexLiteralOccurrence(string RelativeFilePath, int LineNumber, string Pattern);
}
