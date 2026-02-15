namespace SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

/// <summary>
/// Provides deterministic polling assertions for Docker integration tests.
/// </summary>
internal static class DockerAssertions
{
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

				string content = File.ReadAllText(filePath);
				return content.Contains(expectedText, StringComparison.Ordinal);
			},
			timeout,
			$"Timed out waiting for '{expectedText}' in file '{filePath}'.");
	}
}
