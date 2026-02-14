namespace SuwayomiSourceMerge.UnitTests.Application.Watching;

using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="NoOpMergeScanRequestHandler"/>.
/// </summary>
public sealed class NoOpMergeScanRequestHandlerTests
{
	/// <summary>
	/// Verifies dispatch returns success and emits a diagnostic log.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Expected_ShouldReturnSuccess()
	{
		RecordingLogger logger = new();
		NoOpMergeScanRequestHandler handler = new(logger);

		MergeScanDispatchOutcome outcome = handler.DispatchMergeScan("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Contains(logger.Events, static entry => entry.EventId == "merge.dispatch.deferred");
	}

	/// <summary>
	/// Verifies cancellation is honored before logging.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Edge_ShouldThrow_WhenCancellationRequested()
	{
		NoOpMergeScanRequestHandler handler = new(new RecordingLogger());
		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.Cancel();

		Assert.Throws<OperationCanceledException>(
			() => handler.DispatchMergeScan("force-refresh", force: true, cancellationTokenSource.Token));
	}

	/// <summary>
	/// Verifies guard clauses reject invalid constructor and request inputs.
	/// </summary>
	[Fact]
	public void ConstructorAndDispatch_Failure_ShouldThrow_WhenInputsInvalid()
	{
		Assert.Throws<ArgumentNullException>(() => new NoOpMergeScanRequestHandler(null!));

		NoOpMergeScanRequestHandler handler = new(new RecordingLogger());
		Assert.ThrowsAny<ArgumentException>(() => handler.DispatchMergeScan("", force: false));
	}
}
