using SuwayomiSourceMerge.Application.Cancellation;

namespace SuwayomiSourceMerge.Application.Watching;

/// <summary>
/// Coalesces merge-scan requests into one pending request and dispatches under interval/retry gates.
/// </summary>
internal sealed class MergeScanRequestCoalescer : IMergeScanRequestCoalescer
{
	/// <summary>
	/// Synchronization lock for pending-request and timing state.
	/// </summary>
	private readonly object _syncRoot = new();

	/// <summary>
	/// Downstream dispatch target.
	/// </summary>
	private readonly IMergeScanRequestHandler _requestHandler;

	/// <summary>
	/// Minimum interval between successful dispatches.
	/// </summary>
	private readonly TimeSpan _minSecondsBetweenScans;

	/// <summary>
	/// Delay applied after busy/mixed/failure outcomes before retrying.
	/// </summary>
	private readonly TimeSpan _retryDelay;

	/// <summary>
	/// Pending request reason text.
	/// </summary>
	private string? _pendingReason;

	/// <summary>
	/// Monotonic version incremented whenever the pending request is updated.
	/// </summary>
	private int _pendingVersion;

	/// <summary>
	/// Pending force flag.
	/// </summary>
	private bool _pendingForce;

	/// <summary>
	/// Tracks whether a dispatch call is currently executing.
	/// </summary>
	private bool _dispatchInProgress;

	/// <summary>
	/// Timestamp of the most recent successful dispatch.
	/// </summary>
	private DateTimeOffset? _lastSuccessUtc;

	/// <summary>
	/// Earliest timestamp when the next retry may occur after busy/mixed/failure outcomes.
	/// </summary>
	private DateTimeOffset? _nextRetryUtc;

	/// <summary>
	/// Initializes a new instance of the <see cref="MergeScanRequestCoalescer"/> class.
	/// </summary>
	/// <param name="requestHandler">Downstream merge-scan request handler.</param>
	/// <param name="minSecondsBetweenScans">Minimum interval between successful dispatches in seconds.</param>
	/// <param name="retryDelaySeconds">Retry delay applied after busy/mixed/failure outcomes in seconds.</param>
	public MergeScanRequestCoalescer(
		IMergeScanRequestHandler requestHandler,
		int minSecondsBetweenScans,
		int retryDelaySeconds)
	{
		_requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
		if (minSecondsBetweenScans < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(minSecondsBetweenScans), "Minimum seconds between scans must be >= 0.");
		}

		if (retryDelaySeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(retryDelaySeconds), "Retry delay seconds must be > 0.");
		}

		_minSecondsBetweenScans = TimeSpan.FromSeconds(minSecondsBetweenScans);
		_retryDelay = TimeSpan.FromSeconds(retryDelaySeconds);
	}

	/// <inheritdoc />
	public bool HasPendingRequest
	{
		get
		{
			lock (_syncRoot)
			{
				return !string.IsNullOrWhiteSpace(_pendingReason);
			}
		}
	}

	/// <inheritdoc />
	public void RequestScan(string reason, bool force = false)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		lock (_syncRoot)
		{
			// Latest request semantics: newest reason and force replace older pending values.
			_pendingReason = reason;
			_pendingForce = force;
			_pendingVersion++;
		}
	}

	/// <inheritdoc />
	public MergeScanDispatchOutcome DispatchPending(
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken = default)
	{
		string pendingReason;
		bool pendingForce;
		int pendingVersion;

		lock (_syncRoot)
		{
			if (string.IsNullOrWhiteSpace(_pendingReason))
			{
				return MergeScanDispatchOutcome.NoPendingRequest;
			}

			if (_dispatchInProgress)
			{
				return MergeScanDispatchOutcome.Busy;
			}

			if (_nextRetryUtc.HasValue && nowUtc < _nextRetryUtc.Value)
			{
				return MergeScanDispatchOutcome.SkippedDueToRetryDelay;
			}

			if (_lastSuccessUtc.HasValue &&
				nowUtc - _lastSuccessUtc.Value < _minSecondsBetweenScans)
			{
				return MergeScanDispatchOutcome.SkippedDueToMinInterval;
			}

			pendingReason = _pendingReason!;
			pendingForce = _pendingForce;
			pendingVersion = _pendingVersion;
			_dispatchInProgress = true;
		}

		MergeScanDispatchOutcome outcome;
		try
		{
			outcome = _requestHandler.DispatchMergeScan(pendingReason, pendingForce, cancellationToken);
		}
		catch (OperationCanceledException exception) when (CancellationClassification.IsCooperative(exception, cancellationToken))
		{
			lock (_syncRoot)
			{
				_dispatchInProgress = false;
			}

			throw;
		}
		catch
		{
			outcome = MergeScanDispatchOutcome.Failure;
		}

		lock (_syncRoot)
		{
			_dispatchInProgress = false;
			if (outcome == MergeScanDispatchOutcome.Success)
			{
				if (_pendingVersion == pendingVersion)
				{
					_pendingReason = null;
					_pendingForce = false;
				}

				_nextRetryUtc = null;
				_lastSuccessUtc = nowUtc;
				return MergeScanDispatchOutcome.Success;
			}

			if (outcome == MergeScanDispatchOutcome.Busy ||
				outcome == MergeScanDispatchOutcome.Mixed ||
				outcome == MergeScanDispatchOutcome.Failure)
			{
				_nextRetryUtc = nowUtc + _retryDelay;
				return outcome;
			}

			_nextRetryUtc = nowUtc + _retryDelay;
			return MergeScanDispatchOutcome.Failure;
		}
	}
}
