namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="NoOpMetadataApiRequestThrottle"/>.
/// </summary>
public sealed class NoOpMetadataApiRequestThrottleTests
{
	/// <summary>
	/// Verifies delegates run immediately and return their result.
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_Expected_ShouldExecuteOperationImmediately()
	{
		NoOpMetadataApiRequestThrottle throttle = new();
		int executeCount = 0;

		await throttle.ExecuteAsync(
			token =>
			{
				executeCount++;
				return Task.CompletedTask;
			});
		int value = await throttle.ExecuteAsync(
			token =>
			{
				executeCount++;
				return Task.FromResult(42);
			});

		Assert.Equal(2, executeCount);
		Assert.Equal(42, value);
	}

	/// <summary>
	/// Verifies canceled tokens are honored before delegate execution.
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_Edge_ShouldThrowOperationCanceledException_WhenTokenIsCanceled()
	{
		NoOpMetadataApiRequestThrottle throttle = new();
		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.Cancel();

		bool operationInvoked = false;
		await Assert.ThrowsAsync<OperationCanceledException>(
			() => throttle.ExecuteAsync(
				token =>
				{
					operationInvoked = true;
					return Task.CompletedTask;
				},
				cancellationTokenSource.Token));

		Assert.False(operationInvoked);
	}

	/// <summary>
	/// Verifies guard clauses and delegate failure passthrough behavior.
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_Failure_ShouldThrow_WhenInputsOrDelegateAreInvalid()
	{
		NoOpMetadataApiRequestThrottle throttle = new();

		await Assert.ThrowsAsync<ArgumentNullException>(
			() => throttle.ExecuteAsync((Func<CancellationToken, Task>)null!));
		await Assert.ThrowsAsync<ArgumentNullException>(
			() => throttle.ExecuteAsync<int>((Func<CancellationToken, Task<int>>)null!));

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => throttle.ExecuteAsync(
				token => throw new InvalidOperationException("simulated failure")));
	}
}
