namespace SuwayomiSourceMerge.UnitTests.Application.Watching;

using SuwayomiSourceMerge.Application.Watching;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="MergeScanRequestCoalescer"/>.
/// </summary>
public sealed class MergeScanRequestCoalescerTests
{
	/// <summary>
	/// Verifies one pending request dispatches successfully and clears pending state.
	/// </summary>
	[Fact]
	public void DispatchPending_Expected_ShouldDispatchAndClearPending_WhenHandlerSucceeds()
	{
		RecordingMergeScanRequestHandler handler = new([MergeScanDispatchOutcome.Success]);
		MergeScanRequestCoalescer coalescer = new(handler, minSecondsBetweenScans: 15, retryDelaySeconds: 30);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		coalescer.RequestScan("initial", force: false);
		MergeScanDispatchOutcome outcome = coalescer.DispatchPending(now);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.False(coalescer.HasPendingRequest);
		Assert.Single(handler.Calls);
		Assert.Equal("initial", handler.Calls[0].Reason);
	}

	/// <summary>
	/// Verifies burst requests coalesce to one pending request with latest reason and latest force.
	/// </summary>
	[Fact]
	public void RequestScan_Edge_ShouldCoalesceLatestReasonAndForce_WhenBurstRequestsArrive()
	{
		RecordingMergeScanRequestHandler handler = new([MergeScanDispatchOutcome.Success]);
		MergeScanRequestCoalescer coalescer = new(handler, minSecondsBetweenScans: 0, retryDelaySeconds: 5);

		coalescer.RequestScan("first", force: false);
		coalescer.RequestScan("second", force: true);
		coalescer.RequestScan("third", force: false);
		MergeScanDispatchOutcome outcome = coalescer.DispatchPending(DateTimeOffset.UtcNow);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Single(handler.Calls);
		Assert.Equal("third", handler.Calls[0].Reason);
		Assert.False(handler.Calls[0].Force);
	}

	/// <summary>
	/// Verifies minimum-interval gating suppresses immediate redispatch after success.
	/// </summary>
	[Fact]
	public void DispatchPending_Edge_ShouldSkip_WhenMinIntervalNotReached()
	{
		RecordingMergeScanRequestHandler handler = new([MergeScanDispatchOutcome.Success, MergeScanDispatchOutcome.Success]);
		MergeScanRequestCoalescer coalescer = new(handler, minSecondsBetweenScans: 60, retryDelaySeconds: 5);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		coalescer.RequestScan("one", force: false);
		Assert.Equal(MergeScanDispatchOutcome.Success, coalescer.DispatchPending(now));

		coalescer.RequestScan("two", force: false);
		Assert.Equal(MergeScanDispatchOutcome.SkippedDueToMinInterval, coalescer.DispatchPending(now.AddSeconds(10)));
		Assert.True(coalescer.HasPendingRequest);
		Assert.Single(handler.Calls);
	}

	/// <summary>
	/// Verifies busy results keep pending work and enforce retry delay before next attempt.
	/// </summary>
	[Fact]
	public void DispatchPending_Failure_ShouldApplyRetryDelay_WhenHandlerReturnsBusy()
	{
		RecordingMergeScanRequestHandler handler = new([MergeScanDispatchOutcome.Busy, MergeScanDispatchOutcome.Success]);
		MergeScanRequestCoalescer coalescer = new(handler, minSecondsBetweenScans: 0, retryDelaySeconds: 30);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		coalescer.RequestScan("busy-path", force: false);
		Assert.Equal(MergeScanDispatchOutcome.Busy, coalescer.DispatchPending(now));
		Assert.True(coalescer.HasPendingRequest);

		Assert.Equal(MergeScanDispatchOutcome.SkippedDueToRetryDelay, coalescer.DispatchPending(now.AddSeconds(5)));
		Assert.Equal(MergeScanDispatchOutcome.Success, coalescer.DispatchPending(now.AddSeconds(31)));
		Assert.False(coalescer.HasPendingRequest);
		Assert.Equal(2, handler.Calls.Count);
	}

	/// <summary>
	/// Verifies handler exceptions are contained as failure outcomes and retried after delay.
	/// </summary>
	[Fact]
	public void DispatchPending_Failure_ShouldMapExceptionToFailureAndRetry_WhenHandlerThrows()
	{
		ThrowOnceMergeScanRequestHandler handler = new();
		MergeScanRequestCoalescer coalescer = new(handler, minSecondsBetweenScans: 0, retryDelaySeconds: 5);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		coalescer.RequestScan("throwing-request", force: false);
		Assert.Equal(MergeScanDispatchOutcome.Failure, coalescer.DispatchPending(now));
		Assert.True(coalescer.HasPendingRequest);
		Assert.Equal(MergeScanDispatchOutcome.SkippedDueToRetryDelay, coalescer.DispatchPending(now.AddSeconds(1)));
		Assert.Equal(MergeScanDispatchOutcome.Success, coalescer.DispatchPending(now.AddSeconds(6)));
		Assert.False(coalescer.HasPendingRequest);
		Assert.Equal(2, handler.DispatchCalls);
	}

	/// <summary>
	/// Verifies requests queued during an in-flight dispatch remain pending and use latest force semantics.
	/// </summary>
	[Fact]
	public async Task DispatchPending_Edge_ShouldRetainLatestPending_WhenRequestArrivesDuringDispatch()
	{
		BlockingMergeScanRequestHandler handler = new();
		MergeScanRequestCoalescer coalescer = new(handler, minSecondsBetweenScans: 0, retryDelaySeconds: 5);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		coalescer.RequestScan("initial", force: true);
		Task<MergeScanDispatchOutcome> firstDispatchTask = Task.Run(() => coalescer.DispatchPending(now));
		Assert.True(handler.DispatchStarted.Wait(TimeSpan.FromSeconds(5)));

		coalescer.RequestScan("latest", force: false);
		handler.AllowDispatch.Set();

		MergeScanDispatchOutcome firstOutcome = await firstDispatchTask.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Equal(MergeScanDispatchOutcome.Success, firstOutcome);
		Assert.True(coalescer.HasPendingRequest);

		MergeScanDispatchOutcome secondOutcome = coalescer.DispatchPending(now.AddSeconds(6));
		Assert.Equal(MergeScanDispatchOutcome.Success, secondOutcome);
		Assert.False(coalescer.HasPendingRequest);
		Assert.Equal(2, handler.Calls.Count);
		Assert.Equal("initial", handler.Calls[0].Reason);
		Assert.True(handler.Calls[0].Force);
		Assert.Equal("latest", handler.Calls[1].Reason);
		Assert.False(handler.Calls[1].Force);
	}

	/// <summary>
	/// Verifies guard clauses reject invalid constructor and request inputs.
	/// </summary>
	[Fact]
	public void ConstructorAndRequest_Failure_ShouldThrow_WhenInputsInvalid()
	{
		RecordingMergeScanRequestHandler handler = new([MergeScanDispatchOutcome.Success]);

		Assert.Throws<ArgumentNullException>(() => new MergeScanRequestCoalescer(null!, 0, 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => new MergeScanRequestCoalescer(handler, -1, 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => new MergeScanRequestCoalescer(handler, 0, 0));

		MergeScanRequestCoalescer coalescer = new(handler, 0, 1);
		Assert.ThrowsAny<ArgumentException>(() => coalescer.RequestScan(""));
	}

	/// <summary>
	/// Records merge request handler calls and returns configured outcomes in sequence.
	/// </summary>
	private sealed class RecordingMergeScanRequestHandler : IMergeScanRequestHandler
	{
		/// <summary>
		/// Queue of outcomes consumed by dispatch calls.
		/// </summary>
		private readonly Queue<MergeScanDispatchOutcome> _outcomes;

		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingMergeScanRequestHandler"/> class.
		/// </summary>
		/// <param name="outcomes">Outcomes returned by dispatch calls in order.</param>
		public RecordingMergeScanRequestHandler(IReadOnlyList<MergeScanDispatchOutcome> outcomes)
		{
			ArgumentNullException.ThrowIfNull(outcomes);
			_outcomes = new Queue<MergeScanDispatchOutcome>(outcomes);
		}

		/// <summary>
		/// Gets dispatched call records.
		/// </summary>
		public List<(string Reason, bool Force)> Calls
		{
			get;
		} = [];

		/// <inheritdoc />
		public MergeScanDispatchOutcome DispatchMergeScan(string reason, bool force, CancellationToken cancellationToken = default)
		{
			Calls.Add((reason, force));
			if (_outcomes.Count == 0)
			{
				return MergeScanDispatchOutcome.Success;
			}

			return _outcomes.Dequeue();
		}
	}

	/// <summary>
	/// Handler that throws once, then succeeds.
	/// </summary>
	private sealed class ThrowOnceMergeScanRequestHandler : IMergeScanRequestHandler
	{
		/// <summary>
		/// Gets total dispatch call count.
		/// </summary>
		public int DispatchCalls
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public MergeScanDispatchOutcome DispatchMergeScan(string reason, bool force, CancellationToken cancellationToken = default)
		{
			DispatchCalls++;
			if (DispatchCalls == 1)
			{
				throw new InvalidOperationException("simulated failure");
			}

			return MergeScanDispatchOutcome.Success;
		}
	}

	/// <summary>
	/// Handler that blocks dispatch until signaled.
	/// </summary>
	private sealed class BlockingMergeScanRequestHandler : IMergeScanRequestHandler
	{
		/// <summary>
		/// Signals when dispatch starts.
		/// </summary>
		public ManualResetEventSlim DispatchStarted
		{
			get;
		} = new(false);

		/// <summary>
		/// Signals when dispatch may continue.
		/// </summary>
		public ManualResetEventSlim AllowDispatch
		{
			get;
		} = new(false);

		/// <summary>
		/// Gets recorded call sequence.
		/// </summary>
		public List<(string Reason, bool Force)> Calls
		{
			get;
		} = [];

		/// <inheritdoc />
		public MergeScanDispatchOutcome DispatchMergeScan(string reason, bool force, CancellationToken cancellationToken = default)
		{
			Calls.Add((reason, force));
			DispatchStarted.Set();
			AllowDispatch.Wait(cancellationToken);
			return MergeScanDispatchOutcome.Success;
		}
	}
}
