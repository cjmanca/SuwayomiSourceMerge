namespace SuwayomiSourceMerge.UnitTests.Application.Cancellation;

using SuwayomiSourceMerge.Application.Cancellation;

/// <summary>
/// Verifies cooperative cancellation classification behavior.
/// </summary>
public sealed class CancellationClassificationTests
{
	/// <summary>
	/// Verifies matching canceled caller/exception token is classified as cooperative.
	/// </summary>
	[Fact]
	public void IsCooperative_Expected_ShouldReturnTrue_WhenExceptionTokenMatchesCallerToken()
	{
		using CancellationTokenSource tokenSource = new();
		tokenSource.Cancel();
		OperationCanceledException exception = new("cooperative", tokenSource.Token);

		Assert.True(CancellationClassification.IsCooperative(exception, tokenSource.Token));
	}

	/// <summary>
	/// Verifies mismatched exception token is classified as non-cooperative.
	/// </summary>
	[Fact]
	public void IsCooperative_Failure_ShouldReturnFalse_WhenExceptionTokenDiffers()
	{
		using CancellationTokenSource callerTokenSource = new();
		callerTokenSource.Cancel();
		using CancellationTokenSource differentTokenSource = new();
		differentTokenSource.Cancel();
		OperationCanceledException exception = new("different token", differentTokenSource.Token);

		Assert.False(CancellationClassification.IsCooperative(exception, callerTokenSource.Token));
	}

	/// <summary>
	/// Verifies tokenless cancellation exceptions are classified as cooperative when caller token is canceled.
	/// </summary>
	[Fact]
	public void IsCooperative_Expected_ShouldReturnTrue_WhenExceptionTokenIsNotCancelableAndCallerTokenIsCanceled()
	{
		using CancellationTokenSource callerTokenSource = new();
		callerTokenSource.Cancel();
		OperationCanceledException exception = new("tokenless cancellation", CancellationToken.None);

		Assert.True(CancellationClassification.IsCooperative(exception, callerTokenSource.Token));
	}

	/// <summary>
	/// Verifies tokenless cancellation exceptions are non-cooperative when caller token was not canceled.
	/// </summary>
	[Fact]
	public void IsCooperative_Failure_ShouldReturnFalse_WhenCallerTokenIsNotCanceled()
	{
		using CancellationTokenSource callerTokenSource = new();
		OperationCanceledException exception = new("tokenless cancellation", CancellationToken.None);

		Assert.False(CancellationClassification.IsCooperative(exception, callerTokenSource.Token));
	}
}
