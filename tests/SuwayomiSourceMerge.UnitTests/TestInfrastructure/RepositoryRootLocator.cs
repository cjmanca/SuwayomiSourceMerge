namespace SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Resolves repository root paths for repository-policy tests.
/// </summary>
internal static class RepositoryRootLocator
{
	/// <summary>
	/// Locates the repository root by walking upward from the test output directory.
	/// </summary>
	/// <returns>Absolute repository root path.</returns>
	/// <exception cref="InvalidOperationException">Thrown when repository root markers are not found.</exception>
	public static string FindRepositoryRoot()
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
}
