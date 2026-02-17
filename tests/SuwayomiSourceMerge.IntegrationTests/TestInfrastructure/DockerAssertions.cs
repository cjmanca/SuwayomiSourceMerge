namespace SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

/// <summary>
/// Provides deterministic polling assertions for Docker integration tests.
/// </summary>
internal static class DockerAssertions
{
	/// <summary>
	/// Counts lines in one file that match a predicate, retrying transient read-access failures until timeout.
	/// </summary>
	/// <param name="filePath">File path.</param>
	/// <param name="predicate">Line predicate.</param>
	/// <param name="timeout">Timeout window for transient read retries.</param>
	/// <param name="failureMessage">Failure message.</param>
	/// <returns>Matching line count.</returns>
	public static int CountFileLinesMatching(
		string filePath,
		Func<string, bool> predicate,
		TimeSpan timeout,
		string failureMessage)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		ArgumentNullException.ThrowIfNull(predicate);
		ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be > 0.");
		}

		if (!File.Exists(filePath))
		{
			return 0;
		}

		DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
		Exception? lastException = null;

		while (DateTimeOffset.UtcNow <= deadline)
		{
			try
			{
				return ReadLinesShared(filePath)
					.Count(predicate);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				lastException = exception;
			}

			// Intentional synchronous polling: keeps integration assertions deterministic with sync test call sites.
			Thread.Sleep(TimeSpan.FromMilliseconds(250));
		}

		if (lastException is not null)
		{
			throw new Xunit.Sdk.XunitException($"{failureMessage} Last exception: {lastException.Message}");
		}

		throw new Xunit.Sdk.XunitException(failureMessage);
	}

	/// <summary>
	/// Waits until the given predicate returns <see langword="true"/> or times out.
	/// </summary>
	/// <param name="predicate">Condition predicate.</param>
	/// <param name="timeout">Timeout window.</param>
	/// <param name="failureMessage">Failure message.</param>
	public static void WaitForCondition(
		Func<bool> predicate,
		TimeSpan timeout,
		string failureMessage)
	{
		ArgumentNullException.ThrowIfNull(predicate);
		ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be > 0.");
		}

		DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
		Exception? lastException = null;

		while (DateTimeOffset.UtcNow <= deadline)
		{
			try
			{
				if (predicate())
				{
					return;
				}
			}
			catch (Exception exception)
			{
				lastException = exception;
			}

			// Intentional synchronous polling: keeps integration assertions deterministic with sync test call sites.
			Thread.Sleep(TimeSpan.FromMilliseconds(250));
		}

		if (lastException is not null)
		{
			throw new Xunit.Sdk.XunitException($"{failureMessage} Last exception: {lastException.Message}");
		}

		throw new Xunit.Sdk.XunitException(failureMessage);
	}

	/// <summary>
	/// Waits until a file exists and contains the expected text.
	/// </summary>
	/// <param name="filePath">File path.</param>
	/// <param name="expectedText">Expected text fragment.</param>
	/// <param name="timeout">Timeout window.</param>
	public static void WaitForFileContains(
		string filePath,
		string expectedText,
		TimeSpan timeout)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(expectedText);

		WaitForCondition(
			() =>
			{
				if (!File.Exists(filePath))
				{
					return false;
				}

				string content = ReadAllTextShared(filePath);
				return content.Contains(expectedText, StringComparison.Ordinal);
			},
			timeout,
			$"Timed out waiting for '{expectedText}' in file '{filePath}'.");
	}

	/// <summary>
	/// Reads one text file using shared read/write semantics.
	/// </summary>
	/// <param name="filePath">File path.</param>
	/// <returns>File text content.</returns>
	private static string ReadAllTextShared(string filePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

		using FileStream stream = new(
			filePath,
			FileMode.Open,
			FileAccess.Read,
			FileShare.ReadWrite | FileShare.Delete);
		using StreamReader reader = new(stream);
		return reader.ReadToEnd();
	}

	/// <summary>
	/// Reads one text file line-by-line using shared read/write semantics.
	/// </summary>
	/// <param name="filePath">File path.</param>
	/// <returns>File lines.</returns>
	private static IEnumerable<string> ReadLinesShared(string filePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

		using FileStream stream = new(
			filePath,
			FileMode.Open,
			FileAccess.Read,
			FileShare.ReadWrite | FileShare.Delete);
		using StreamReader reader = new(stream);

		while (reader.ReadLine() is string line)
		{
			yield return line;
		}
	}
}
