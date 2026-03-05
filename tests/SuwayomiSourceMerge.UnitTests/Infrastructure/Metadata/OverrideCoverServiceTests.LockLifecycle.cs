namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies keyed cover-write lock lifecycle behavior for <see cref="OverrideCoverService"/>.
/// </summary>
public sealed partial class OverrideCoverServiceTests
{
	/// <summary>
	/// Verifies unique-path lock entries are reclaimed after successful writes.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Edge_ShouldReclaimKeyedLockEntries_AfterUniquePathWrites()
	{
		int baselineLockCount = OverrideCoverService.GetCoverWriteLockCountForTests();
		using TemporaryDirectory temporaryDirectory = new();
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, _onePixelJpegPayload, "image/jpeg"));
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		const int requestCount = 24;
		for (int index = 0; index < requestCount; index++)
		{
			string preferredOverrideDirectoryPath = CreateDirectory(
				temporaryDirectory.Path,
				"override",
				"priority",
				$"Manga Title {index}");
			OverrideCoverRequest request = new(
				preferredOverrideDirectoryPath,
				[preferredOverrideDirectoryPath],
				"covers/sample.jpg");

			OverrideCoverResult result = await service.EnsureCoverJpgAsync(request);
			Assert.Equal(OverrideCoverOutcome.WrittenDownloadedJpeg, result.Outcome);
		}

		Assert.Equal(requestCount, handler.CallCount);
		await AssertCoverWriteLockCountEventuallyAsync(baselineLockCount);
	}

	/// <summary>
	/// Verifies cancellation while waiting for a keyed write lock does not leak lock references.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Failure_ShouldReclaimLockReference_WhenCanceledWhileWaitingForWriteLock()
	{
		int baselineLockCount = OverrideCoverService.GetCoverWriteLockCountForTests();
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		ConcurrentGateHttpMessageHandler handler = new(_onePixelJpegPayload, expectedCallCount: 1);
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);
		OverrideCoverRequest request = new(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			"covers/sample.jpg");

		Task<OverrideCoverResult> firstTask = service.EnsureCoverJpgAsync(request);
		await handler.WaitUntilExpectedCallsAsync();

		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => service.EnsureCoverJpgAsync(request, cancellationTokenSource.Token));

		handler.ReleaseResponses();
		OverrideCoverResult firstResult = await firstTask;
		Assert.Equal(OverrideCoverOutcome.WrittenDownloadedJpeg, firstResult.Outcome);
		Assert.Equal(1, handler.CallCount);

		await AssertCoverWriteLockCountEventuallyAsync(baselineLockCount);
	}

	/// <summary>
	/// Verifies write-failed paths still release keyed write locks for subsequent reuse.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Failure_ShouldReclaimKeyedLockEntry_WhenWriteStageFails()
	{
		int baselineLockCount = OverrideCoverService.GetCoverWriteLockCountForTests();
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, _onePixelJpegPayload, "image/jpeg"));
		using HttpClient httpClient = new(handler);
		FaultInjectingCoverFileOperations fileOperations = new(
			moveOverride: static (_, _, _) => throw new NotSupportedException("simulated move failure"));
		using OverrideCoverService service = new(httpClient, coverBaseUri: null, fileOperations);
		OverrideCoverRequest request = new(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			"covers/sample.jpg");

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(request);

		Assert.Equal(OverrideCoverOutcome.WriteFailed, result.Outcome);
		Assert.Equal(1, handler.CallCount);
		await AssertCoverWriteLockCountEventuallyAsync(baselineLockCount);
	}

	/// <summary>
	/// Verifies repeated same-key contention does not trigger dispose-while-in-use races.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Edge_ShouldAvoidObjectDisposedException_WhenAcquireAndReleaseContentionRepeats()
	{
		int baselineLockCount = OverrideCoverService.GetCoverWriteLockCountForTests();
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string coverPath = Path.Combine(preferredOverrideDirectoryPath, "cover.jpg");

		const int rounds = 30;
		const int callersPerRound = 12;
		for (int round = 0; round < rounds; round++)
		{
			if (File.Exists(coverPath))
			{
				File.Delete(coverPath);
			}

			ConcurrentGateHttpMessageHandler handler = new(_onePixelJpegPayload, expectedCallCount: 1);
			using HttpClient httpClient = new(handler);
			using OverrideCoverService service = new(httpClient, coverBaseUri: null);
			OverrideCoverRequest request = new(
				preferredOverrideDirectoryPath,
				[preferredOverrideDirectoryPath],
				"covers/sample.jpg");

			Task<OverrideCoverResult>[] tasks = Enumerable
				.Range(0, callersPerRound)
				.Select(_ => service.EnsureCoverJpgAsync(request))
				.ToArray();

			await handler.WaitUntilExpectedCallsAsync();
			handler.ReleaseResponses();

			OverrideCoverResult[] results = await Task.WhenAll(tasks);
			int writtenCount = results.Count(
				static result => result.Outcome is OverrideCoverOutcome.WrittenDownloadedJpeg or OverrideCoverOutcome.WrittenConvertedJpeg);
			int alreadyExistsCount = results.Count(static result => result.Outcome == OverrideCoverOutcome.AlreadyExists);

			Assert.Equal(1, writtenCount);
			Assert.Equal(callersPerRound - 1, alreadyExistsCount);
			Assert.Equal(1, handler.CallCount);
			Assert.True(File.Exists(coverPath));
		}

		await AssertCoverWriteLockCountEventuallyAsync(baselineLockCount);
	}

	/// <summary>
	/// Waits for the keyed write-lock dictionary to return to one expected count.
	/// </summary>
	/// <param name="expectedCount">Expected lock count.</param>
	private static async Task AssertCoverWriteLockCountEventuallyAsync(int expectedCount)
	{
		const int maxAttempts = 80;
		for (int attempt = 0; attempt < maxAttempts; attempt++)
		{
			if (OverrideCoverService.GetCoverWriteLockCountForTests() == expectedCount)
			{
				return;
			}

			await Task.Delay(10);
		}

		Assert.Equal(expectedCount, OverrideCoverService.GetCoverWriteLockCountForTests());
	}
}
