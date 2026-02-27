namespace SuwayomiSourceMerge.UnitTests.Application.Watching;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="ProductionMergeScanRequestHandler"/>.
/// </summary>
public sealed class ProductionMergeScanRequestHandlerTests
{
	/// <summary>
	/// Verifies successful workflow dispatch returns success and emits completion logs.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Expected_ShouldReturnSuccess_WhenWorkflowSucceeds()
	{
		RecordingLogger logger = new();
		RecordingMergeWorkflow workflow = new(MergeScanDispatchOutcome.Success);
		ProductionMergeScanRequestHandler handler = new(workflow, logger);

		MergeScanDispatchOutcome outcome = handler.DispatchMergeScan("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Equal(1, workflow.DispatchCalls);
		Assert.Contains(logger.Events, static entry => entry.EventId == "merge.dispatch.started");
		Assert.Contains(logger.Events, static entry => entry.EventId == "merge.dispatch.completed");
	}

	/// <summary>
	/// Verifies cancellation is propagated before workflow execution.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Edge_ShouldThrow_WhenCancellationRequested()
	{
		ProductionMergeScanRequestHandler handler = new(
			new RecordingMergeWorkflow(MergeScanDispatchOutcome.Success),
			new RecordingLogger());
		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.Cancel();

		Assert.Throws<OperationCanceledException>(
			() => handler.DispatchMergeScan("forced", force: true, cancellationTokenSource.Token));
	}

	/// <summary>
	/// Verifies busy workflow outcomes are returned and logged as busy dispatch warnings.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Failure_ShouldReturnBusy_WhenWorkflowReportsBusy()
	{
		RecordingLogger logger = new();
		ProductionMergeScanRequestHandler handler = new(new RecordingMergeWorkflow(MergeScanDispatchOutcome.Busy), logger);

		MergeScanDispatchOutcome outcome = handler.DispatchMergeScan("override-force:Title", force: true);

		Assert.Equal(MergeScanDispatchOutcome.Busy, outcome);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "merge.dispatch.busy" && entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Warning);
	}

	/// <summary>
	/// Verifies mixed workflow outcomes are returned and logged as warning-level mixed dispatch events.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Failure_ShouldReturnMixed_WhenWorkflowReportsMixedOutcome()
	{
		RecordingLogger logger = new();
		ProductionMergeScanRequestHandler handler = new(new RecordingMergeWorkflow(MergeScanDispatchOutcome.Mixed), logger);

		MergeScanDispatchOutcome outcome = handler.DispatchMergeScan("override-force:Title", force: true);

		Assert.Equal(MergeScanDispatchOutcome.Mixed, outcome);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "merge.dispatch.mixed" && entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Warning);
	}

	/// <summary>
	/// Verifies non-success workflow outcomes are returned and logged as failed dispatches.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Failure_ShouldReturnFailure_WhenWorkflowReportsFailure()
	{
		RecordingLogger logger = new();
		ProductionMergeScanRequestHandler handler = new(new RecordingMergeWorkflow(MergeScanDispatchOutcome.Failure), logger);

		MergeScanDispatchOutcome outcome = handler.DispatchMergeScan("override-force:Title", force: true);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "merge.dispatch.failed" && entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Error);
	}

	/// <summary>
	/// Verifies non-cancellation workflow exceptions are mapped to failure and emit failed dispatch events.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Failure_ShouldReturnFailureAndLogFailed_WhenWorkflowThrowsNonCancellation()
	{
		RecordingLogger logger = new();
		ProductionMergeScanRequestHandler handler = new(
			new RecordingMergeWorkflow(new InvalidOperationException("simulated workflow failure")),
			logger);

		MergeScanDispatchOutcome outcome = handler.DispatchMergeScan("override-force:Title", force: true);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Contains(logger.Events, static entry => entry.EventId == "merge.dispatch.started");
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "merge.dispatch.failed" &&
				entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Error &&
				entry.Message.Contains("mapped to failure outcome", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies fatal workflow exceptions are rethrown instead of being normalized to failure outcomes.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Failure_ShouldRethrowFatalException_WhenWorkflowThrowsFatal()
	{
		RecordingLogger logger = new();
		ProductionMergeScanRequestHandler handler = new(
			new RecordingMergeWorkflow(new OutOfMemoryException("simulated fatal failure")),
			logger);

		Assert.Throws<OutOfMemoryException>(() => handler.DispatchMergeScan("override-force:Title", force: true));
		Assert.DoesNotContain(logger.Events, static entry => entry.EventId == "merge.dispatch.failed");
	}

	/// <summary>
	/// Verifies non-cooperative cancellation exceptions are mapped to failure and emit failed dispatch events.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Failure_ShouldReturnFailureAndLogFailed_WhenWorkflowThrowsOperationCanceledWithoutTokenCancellation()
	{
		RecordingLogger logger = new();
		ProductionMergeScanRequestHandler handler = new(
			new RecordingMergeWorkflow(new OperationCanceledException("simulated non-cooperative cancellation")),
			logger);

		MergeScanDispatchOutcome outcome = handler.DispatchMergeScan("override-force:Title", force: true, cancellationToken: CancellationToken.None);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "merge.dispatch.failed" &&
				entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Error &&
				entry.Message.Contains("non-cooperative cancellation", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies cancellation with mismatched exception token is treated as non-cooperative even if caller token is canceled during dispatch.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Failure_ShouldReturnFailure_WhenCallerTokenIsCancelledButExceptionTokenDiffers()
	{
		RecordingLogger logger = new();
		using CancellationTokenSource callerTokenSource = new();
		using CancellationTokenSource differentTokenSource = new();
		differentTokenSource.Cancel();
		ProductionMergeScanRequestHandler handler = new(
			new DelegatingMergeWorkflow((_, _, _) =>
			{
				callerTokenSource.Cancel();
				throw new OperationCanceledException("mismatched token", differentTokenSource.Token);
			}),
			logger);

		MergeScanDispatchOutcome outcome = handler.DispatchMergeScan(
			"override-force:Title",
			force: true,
			cancellationToken: callerTokenSource.Token);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "merge.dispatch.failed" &&
				entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Error &&
				entry.Message.Contains("non-cooperative cancellation", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies tokenless cancellation is treated as cooperative when caller token is canceled.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Edge_ShouldRethrowCancellation_WhenCallerTokenIsCanceledAndExceptionIsTokenless()
	{
		RecordingLogger logger = new();
		using CancellationTokenSource callerTokenSource = new();
		ProductionMergeScanRequestHandler handler = new(
			new DelegatingMergeWorkflow((_, _, _) =>
			{
				callerTokenSource.Cancel();
				throw new OperationCanceledException("tokenless cancellation");
			}),
			logger);

		Assert.Throws<OperationCanceledException>(
			() => handler.DispatchMergeScan(
				"override-force:Title",
				force: true,
				cancellationToken: callerTokenSource.Token));
		Assert.DoesNotContain(logger.Events, static entry => entry.EventId == "merge.dispatch.failed");
	}

	/// <summary>
	/// Verifies unexpected workflow outcomes are normalized to failure and logged with failure event semantics.
	/// </summary>
	[Fact]
	public void DispatchMergeScan_Failure_ShouldNormalizeUnexpectedOutcomeToFailure()
	{
		RecordingLogger logger = new();
		ProductionMergeScanRequestHandler handler = new(
			new RecordingMergeWorkflow(MergeScanDispatchOutcome.NoPendingRequest),
			logger);

		MergeScanDispatchOutcome outcome = handler.DispatchMergeScan("override-force:Title", force: true);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "merge.dispatch.failed" &&
				entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Error &&
				entry.Message.Contains("normalized to failure", StringComparison.Ordinal));
	}

	/// <summary>
	/// Workflow test double that returns a configured outcome.
	/// </summary>
	private sealed class RecordingMergeWorkflow : IMergeMountWorkflow
	{
		/// <summary>
		/// Configured outcome.
		/// </summary>
		private readonly MergeScanDispatchOutcome _outcome;

		/// <summary>
		/// Optional exception thrown from dispatch.
		/// </summary>
		private readonly Exception? _exception;

		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingMergeWorkflow"/> class.
		/// </summary>
		/// <param name="outcome">Configured outcome.</param>
		public RecordingMergeWorkflow(MergeScanDispatchOutcome outcome)
		{
			_outcome = outcome;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingMergeWorkflow"/> class.
		/// </summary>
		/// <param name="exception">Exception to throw from dispatch.</param>
		public RecordingMergeWorkflow(Exception exception)
		{
			_exception = exception ?? throw new ArgumentNullException(nameof(exception));
		}

		/// <summary>
		/// Gets dispatch call count.
		/// </summary>
		public int DispatchCalls
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public MergeScanDispatchOutcome RunMergePass(
			string reason,
			bool force,
			CancellationToken cancellationToken = default)
		{
			DispatchCalls++;
			cancellationToken.ThrowIfCancellationRequested();
			if (_exception is not null)
			{
				throw _exception;
			}

			return _outcome;
		}
	}

	/// <summary>
	/// Workflow test double that delegates behavior to one callback.
	/// </summary>
	private sealed class DelegatingMergeWorkflow : IMergeMountWorkflow
	{
		/// <summary>
		/// Dispatch callback.
		/// </summary>
		private readonly Func<string, bool, CancellationToken, MergeScanDispatchOutcome> _callback;

		/// <summary>
		/// Initializes a new instance of the <see cref="DelegatingMergeWorkflow"/> class.
		/// </summary>
		/// <param name="callback">Dispatch callback.</param>
		public DelegatingMergeWorkflow(Func<string, bool, CancellationToken, MergeScanDispatchOutcome> callback)
		{
			_callback = callback ?? throw new ArgumentNullException(nameof(callback));
		}

		/// <inheritdoc />
		public MergeScanDispatchOutcome RunMergePass(string reason, bool force, CancellationToken cancellationToken = default)
		{
			return _callback(reason, force, cancellationToken);
		}
	}
}
