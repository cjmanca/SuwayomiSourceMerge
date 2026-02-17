using SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

namespace SuwayomiSourceMerge.IntegrationTests;

/// <summary>
/// Verifies deterministic polling and timeout diagnostics for <see cref="DockerAssertions"/>.
/// </summary>
public sealed class DockerAssertionsBehaviorTests
{
	/// <summary>
	/// Verifies file-contains polling succeeds when content already includes expected text.
	/// </summary>
	[Fact]
	public void WaitForFileContains_Expected_ShouldReturn_WhenFileContainsExpectedText()
	{
		string tempDirectory = CreateTempDirectory();
		try
		{
			string filePath = Path.Combine(tempDirectory, "daemon.log");
			File.WriteAllText(filePath, "event=\"host.startup\"", System.Text.Encoding.UTF8);

			DockerAssertions.WaitForFileContains(filePath, "event=\"host.startup\"", TimeSpan.FromSeconds(1));
		}
		finally
		{
			DeleteDirectoryBestEffort(tempDirectory);
		}
	}

	/// <summary>
	/// Verifies generic condition polling retries after transient exceptions and succeeds when predicate recovers.
	/// </summary>
	[Fact]
	public void WaitForCondition_Edge_ShouldRetryExceptionsAndSucceed_WhenPredicateRecovers()
	{
		int attempts = 0;
		DockerAssertions.WaitForCondition(
			() =>
			{
				attempts++;
				if (attempts == 1)
				{
					throw new IOException("sharing violation");
				}

				if (attempts == 2)
				{
					throw new UnauthorizedAccessException("denied");
				}

				return true;
			},
			TimeSpan.FromSeconds(2),
			"Expected predicate to eventually recover.");

		Assert.True(attempts >= 3);
	}

	/// <summary>
	/// Verifies file polling timeout includes last read exception diagnostics when reads repeatedly fail.
	/// </summary>
	[Fact]
	public void WaitForFileContains_Failure_ShouldIncludeLastException_WhenReadsKeepFailing()
	{
		string tempDirectory = CreateTempDirectory();
		try
		{
			string filePath = Path.Combine(tempDirectory, "daemon.log");
			File.WriteAllText(filePath, "event=\"different\"", System.Text.Encoding.UTF8);

			using FileStream heldLock = new(
				filePath,
				FileMode.Open,
				FileAccess.ReadWrite,
				FileShare.None);

			Xunit.Sdk.XunitException exception = Assert.Throws<Xunit.Sdk.XunitException>(() =>
				DockerAssertions.WaitForFileContains(
					filePath,
					"event=\"host.startup\"",
					TimeSpan.FromMilliseconds(600)));

			Assert.Contains("Timed out waiting for", exception.Message, StringComparison.Ordinal);
			Assert.Contains("Last exception:", exception.Message, StringComparison.Ordinal);
		}
		finally
		{
			DeleteDirectoryBestEffort(tempDirectory);
		}
	}

	/// <summary>
	/// Verifies line-count polling returns the number of matching lines from one shared-read file.
	/// </summary>
	[Fact]
	public void CountFileLinesMatching_Expected_ShouldReturnMatchingLineCount()
	{
		string tempDirectory = CreateTempDirectory();
		try
		{
			string filePath = Path.Combine(tempDirectory, "commands.log");
			File.WriteAllLines(
				filePath,
				[
					"fusermount3 /ssm/merged/MangaA",
					"echo unrelated",
					"umount /ssm/merged/MangaB",
					"fusermount /ssm/merged/MangaC"
				],
				System.Text.Encoding.UTF8);

			int result = DockerAssertions.CountFileLinesMatching(
				filePath,
				static line => line.StartsWith("fusermount3 ", StringComparison.Ordinal) ||
					line.StartsWith("fusermount ", StringComparison.Ordinal) ||
					line.StartsWith("umount ", StringComparison.Ordinal),
				TimeSpan.FromSeconds(1),
				"Expected matching line count.");

			Assert.Equal(3, result);
		}
		finally
		{
			DeleteDirectoryBestEffort(tempDirectory);
		}
	}

	/// <summary>
	/// Verifies line-count polling retries transient share locks and succeeds once read access recovers.
	/// </summary>
	[Fact]
	public async Task CountFileLinesMatching_Edge_ShouldRetryTransientLockAndSucceed_WhenReadAccessRecovers()
	{
		string tempDirectory = CreateTempDirectory();
		try
		{
			string filePath = Path.Combine(tempDirectory, "commands.log");
			File.WriteAllText(filePath, "umount /ssm/merged/MangaA", System.Text.Encoding.UTF8);

			FileStream heldLock = new(
				filePath,
				FileMode.Open,
				FileAccess.ReadWrite,
				FileShare.None);

			Task releaseTask = Task.Run(() =>
			{
				Thread.Sleep(TimeSpan.FromMilliseconds(350));
				heldLock.Dispose();
			});

			int result = DockerAssertions.CountFileLinesMatching(
				filePath,
				static line => line.StartsWith("umount ", StringComparison.Ordinal),
				TimeSpan.FromSeconds(2),
				"Expected transient lock to recover.");

			await releaseTask;
			Assert.Equal(1, result);
		}
		finally
		{
			DeleteDirectoryBestEffort(tempDirectory);
		}
	}

	/// <summary>
	/// Verifies line-count polling timeout includes last read exception diagnostics when reads repeatedly fail.
	/// </summary>
	[Fact]
	public void CountFileLinesMatching_Failure_ShouldIncludeLastException_WhenReadAccessNeverRecovers()
	{
		string tempDirectory = CreateTempDirectory();
		try
		{
			string filePath = Path.Combine(tempDirectory, "commands.log");
			File.WriteAllText(filePath, "umount /ssm/merged/MangaA", System.Text.Encoding.UTF8);

			using FileStream heldLock = new(
				filePath,
				FileMode.Open,
				FileAccess.ReadWrite,
				FileShare.None);

			Xunit.Sdk.XunitException exception = Assert.Throws<Xunit.Sdk.XunitException>(() =>
				DockerAssertions.CountFileLinesMatching(
					filePath,
					static line => line.StartsWith("umount ", StringComparison.Ordinal),
					TimeSpan.FromMilliseconds(700),
					"Timed out counting command lines."));

			Assert.Contains("Timed out counting command lines.", exception.Message, StringComparison.Ordinal);
			Assert.Contains("Last exception:", exception.Message, StringComparison.Ordinal);
		}
		finally
		{
			DeleteDirectoryBestEffort(tempDirectory);
		}
	}

	/// <summary>
	/// Creates one deterministic unique temporary directory path.
	/// </summary>
	/// <returns>Created temporary directory path.</returns>
	private static string CreateTempDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"ssm-int-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}

	/// <summary>
	/// Deletes one directory tree without surfacing teardown-time cleanup exceptions.
	/// </summary>
	/// <param name="path">Directory path.</param>
	private static void DeleteDirectoryBestEffort(string path)
	{
		if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
		{
			return;
		}

		try
		{
			Directory.Delete(path, recursive: true);
		}
		catch
		{
		}
	}
}
