namespace SuwayomiSourceMerge.UnitTests.Repository;

using System.Text.RegularExpressions;

/// <summary>
/// Verifies repository member naming conventions for const and private static readonly fields.
/// </summary>
public sealed class NamingConventionPolicyTests
{
	/// <summary>
	/// Detects member const declarations using screaming-snake identifier names.
	/// </summary>
	[Fact]
	public void MemberConstFields_ShouldNotUseScreamingSnakeCase()
	{
		string repositoryRoot = FindRepositoryRoot();
		IReadOnlyList<(string File, int Line, string Name)> violations = FindMemberConstScreamingSnakeViolations(repositoryRoot);
		Assert.True(violations.Count == 0, BuildFailureMessage(
			"Detected member const identifiers using screaming-snake naming.",
			violations));
	}

	/// <summary>
	/// Detects private static readonly declarations using uppercase/screaming-snake identifier names.
	/// </summary>
	[Fact]
	public void PrivateStaticReadonlyFields_ShouldNotUseUppercaseIdentifiers()
	{
		string repositoryRoot = FindRepositoryRoot();
		IReadOnlyList<(string File, int Line, string Name)> violations = FindPrivateStaticReadonlyUppercaseViolations(repositoryRoot);
		Assert.True(violations.Count == 0, BuildFailureMessage(
			"Detected private static readonly identifiers using uppercase naming.",
			violations));
	}

	/// <summary>
	/// Resolves the repository root by walking upward from the test output directory.
	/// </summary>
	/// <returns>Absolute path to repository root.</returns>
	/// <exception cref="InvalidOperationException">Thrown when repository markers cannot be resolved.</exception>
	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null)
		{
			string candidateRoot = directory.FullName;
			bool hasSolution = File.Exists(Path.Combine(candidateRoot, "SuwayomiSourceMerge.slnx"));
			bool hasGitDirectory = Directory.Exists(Path.Combine(candidateRoot, ".git"));
			bool hasGitFile = File.Exists(Path.Combine(candidateRoot, ".git"));
			if (hasSolution && (hasGitDirectory || hasGitFile))
			{
				return candidateRoot;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException("Could not locate repository root from test output directory.");
	}

	/// <summary>
	/// Finds member const declarations that use screaming-snake identifier naming.
	/// </summary>
	/// <param name="repositoryRoot">Absolute repository root path.</param>
	/// <returns>Violations with relative file path, line number, and identifier name.</returns>
	private static IReadOnlyList<(string File, int Line, string Name)> FindMemberConstScreamingSnakeViolations(string repositoryRoot)
	{
		const string constDeclarationPattern = "^\\s*(?:public|internal|protected|private)\\s+const\\s+[^=;]+?\\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*=";
		return FindViolations(repositoryRoot, constDeclarationPattern, static name => Regex.IsMatch(name, "^[A-Z0-9_]+$", RegexOptions.CultureInvariant));
	}

	/// <summary>
	/// Finds private static readonly declarations that use uppercase identifier naming.
	/// </summary>
	/// <param name="repositoryRoot">Absolute repository root path.</param>
	/// <returns>Violations with relative file path, line number, and identifier name.</returns>
	private static IReadOnlyList<(string File, int Line, string Name)> FindPrivateStaticReadonlyUppercaseViolations(string repositoryRoot)
	{
		const string readonlyDeclarationPattern = "^\\s*private\\s+static\\s+readonly\\s+[^=;]+?\\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*=";
		return FindViolations(repositoryRoot, readonlyDeclarationPattern, static name => Regex.IsMatch(name, "^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant));
	}

	/// <summary>
	/// Evaluates one declaration pattern and returns identifier violations.
	/// </summary>
	/// <param name="repositoryRoot">Absolute repository root path.</param>
	/// <param name="declarationPattern">Declaration regex with a <c>name</c> capture group.</param>
	/// <param name="isViolation">Predicate that returns whether an identifier violates policy.</param>
	/// <returns>Violations with relative file path, line number, and identifier name.</returns>
	private static IReadOnlyList<(string File, int Line, string Name)> FindViolations(
		string repositoryRoot,
		string declarationPattern,
		Func<string, bool> isViolation)
	{
		Regex declarationRegex = new(declarationPattern, RegexOptions.CultureInvariant);
		List<(string File, int Line, string Name)> violations = [];

		IEnumerable<string> files = Directory.EnumerateFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.OrderBy(path => path, StringComparer.Ordinal);

		foreach (string file in files)
		{
			string relativePath = Path.GetRelativePath(repositoryRoot, file);
			string[] lines = File.ReadAllLines(file);
			for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
			{
				Match match = declarationRegex.Match(lines[lineIndex]);
				if (!match.Success)
				{
					continue;
				}

				string name = match.Groups["name"].Value;
				if (isViolation(name))
				{
					violations.Add((relativePath.Replace('\\', '/'), lineIndex + 1, name));
				}
			}
		}

		return violations;
	}

	/// <summary>
	/// Builds deterministic assertion output for naming violations.
	/// </summary>
	/// <param name="title">Assertion title.</param>
	/// <param name="violations">Violation entries.</param>
	/// <returns>Assertion message.</returns>
	private static string BuildFailureMessage(string title, IReadOnlyList<(string File, int Line, string Name)> violations)
	{
		List<string> lines =
		[
			title
		];

		for (int index = 0; index < violations.Count; index++)
		{
			(string file, int line, string name) = violations[index];
			lines.Add($" - {file}:{line} => {name}");
		}

		return string.Join(Environment.NewLine, lines);
	}
}
