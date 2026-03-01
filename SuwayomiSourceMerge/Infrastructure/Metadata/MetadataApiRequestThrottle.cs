namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Applies completion-anchored pacing between metadata API operations.
/// </summary>
internal sealed class MetadataApiRequestThrottle : IMetadataApiRequestThrottle
{
	/// <summary>
	/// Delay configured between one operation completion and the next operation start.
	/// </summary>
	private readonly TimeSpan _requestDelay;

	/// <summary>
	/// Clock provider used for deterministic pacing decisions.
	/// </summary>
	private readonly Func<DateTimeOffset> _utcNowProvider;

	/// <summary>
	/// Delay callback used to wait for pacing windows.
	/// </summary>
	private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

	/// <summary>
	/// Serializes operation execution to enforce one shared pacing timeline.
	/// </summary>
	private readonly SemaphoreSlim _executionGate = new(1, 1);

	/// <summary>
	/// Next UTC timestamp at which a new operation may begin.
	/// </summary>
	private DateTimeOffset? _nextAllowedStartUtc;

	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataApiRequestThrottle"/> class.
	/// </summary>
	/// <param name="requestDelay">Delay applied between operation completion and the next operation start.</param>
	public MetadataApiRequestThrottle(TimeSpan requestDelay)
		: this(
			requestDelay,
			static () => DateTimeOffset.UtcNow,
			DefaultDelayAsync)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataApiRequestThrottle"/> class.
	/// </summary>
	/// <param name="requestDelay">Delay applied between operation completion and the next operation start.</param>
	/// <param name="utcNowProvider">Clock provider used for pacing decisions.</param>
	/// <param name="delayAsync">Delay callback used when pacing requires waiting.</param>
	internal MetadataApiRequestThrottle(
		TimeSpan requestDelay,
		Func<DateTimeOffset> utcNowProvider,
		Func<TimeSpan, CancellationToken, Task> delayAsync)
	{
		if (requestDelay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(requestDelay),
				requestDelay,
				"Metadata API request delay must be >= 0.");
		}

		_requestDelay = requestDelay;
		_utcNowProvider = utcNowProvider ?? throw new ArgumentNullException(nameof(utcNowProvider));
		_delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
	}

	/// <inheritdoc />
	public Task ExecuteAsync(
		Func<CancellationToken, Task> operation,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(operation);

		return ExecuteAsync<object?>(
			async token =>
			{
				await operation(token).ConfigureAwait(false);
				return null;
			},
			cancellationToken);
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(operation);

		await _executionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await WaitForPacingWindowIfRequired(cancellationToken).ConfigureAwait(false);

			try
			{
				return await operation(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				DateTimeOffset completedAtUtc = _utcNowProvider().ToUniversalTime();
				_nextAllowedStartUtc = completedAtUtc + _requestDelay;
			}
		}
		finally
		{
			_executionGate.Release();
		}
	}

	/// <summary>
	/// Waits until the next operation may start when pacing is enabled and required.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that completes when the current pacing window allows execution.</returns>
	private async Task WaitForPacingWindowIfRequired(CancellationToken cancellationToken)
	{
		if (_requestDelay <= TimeSpan.Zero || !_nextAllowedStartUtc.HasValue)
		{
			return;
		}

		DateTimeOffset nowUtc = _utcNowProvider().ToUniversalTime();
		DateTimeOffset nextAllowedStartUtc = _nextAllowedStartUtc.Value;
		if (nextAllowedStartUtc <= nowUtc)
		{
			return;
		}

		TimeSpan remainingDelay = nextAllowedStartUtc - nowUtc;
		await _delayAsync(remainingDelay, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Executes the default delay behavior for non-test runtime use.
	/// </summary>
	/// <param name="delay">Delay to apply.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that completes after the delay elapses.</returns>
	private static Task DefaultDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
	{
		if (delay <= TimeSpan.Zero)
		{
			return Task.CompletedTask;
		}

		return Task.Delay(delay, cancellationToken);
	}
}
