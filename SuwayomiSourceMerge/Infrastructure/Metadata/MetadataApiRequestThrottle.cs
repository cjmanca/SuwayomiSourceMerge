namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Applies completion-anchored pacing between metadata API operations.
/// </summary>
internal sealed class MetadataApiRequestThrottle : IMetadataApiRequestThrottle
{
	/// <summary>
	/// Minimum delay configured between one operation completion and the next operation start.
	/// </summary>
	private readonly TimeSpan _minimumRequestDelay;

	/// <summary>
	/// Maximum delay configured between one operation completion and the next operation start.
	/// </summary>
	private readonly TimeSpan _maximumRequestDelay;

	/// <summary>
	/// Clock provider used for deterministic pacing decisions.
	/// </summary>
	private readonly Func<DateTimeOffset> _utcNowProvider;

	/// <summary>
	/// Delay callback used to wait for pacing windows.
	/// </summary>
	private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

	/// <summary>
	/// Selects one delay value within the configured pacing range.
	/// </summary>
	private readonly Func<TimeSpan, TimeSpan, TimeSpan> _requestDelaySelector;

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
			requestDelay,
			static () => DateTimeOffset.UtcNow,
			DefaultDelayAsync,
			static (minimumDelay, maximumDelay) => minimumDelay)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataApiRequestThrottle"/> class.
	/// </summary>
	/// <param name="minimumRequestDelay">Minimum delay applied between operation completion and the next operation start.</param>
	/// <param name="maximumRequestDelay">Maximum delay applied between operation completion and the next operation start.</param>
	public MetadataApiRequestThrottle(TimeSpan minimumRequestDelay, TimeSpan maximumRequestDelay)
		: this(
			minimumRequestDelay,
			maximumRequestDelay,
			static () => DateTimeOffset.UtcNow,
			DefaultDelayAsync,
			DefaultSelectDelay)
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
		: this(
			requestDelay,
			requestDelay,
			utcNowProvider,
			delayAsync,
			static (minimumDelay, maximumDelay) => minimumDelay)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataApiRequestThrottle"/> class.
	/// </summary>
	/// <param name="minimumRequestDelay">Minimum delay applied between operation completion and the next operation start.</param>
	/// <param name="maximumRequestDelay">Maximum delay applied between operation completion and the next operation start.</param>
	/// <param name="utcNowProvider">Clock provider used for pacing decisions.</param>
	/// <param name="delayAsync">Delay callback used when pacing requires waiting.</param>
	/// <param name="requestDelaySelector">Selector used to pick the next pacing delay from the configured range.</param>
	internal MetadataApiRequestThrottle(
		TimeSpan minimumRequestDelay,
		TimeSpan maximumRequestDelay,
		Func<DateTimeOffset> utcNowProvider,
		Func<TimeSpan, CancellationToken, Task> delayAsync,
		Func<TimeSpan, TimeSpan, TimeSpan> requestDelaySelector)
	{
		if (minimumRequestDelay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(minimumRequestDelay),
				minimumRequestDelay,
				"Metadata API minimum request delay must be >= 0.");
		}

		if (maximumRequestDelay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(maximumRequestDelay),
				maximumRequestDelay,
				"Metadata API maximum request delay must be >= 0.");
		}

		if (maximumRequestDelay < minimumRequestDelay)
		{
			throw new ArgumentOutOfRangeException(
				nameof(maximumRequestDelay),
				maximumRequestDelay,
				"Metadata API maximum request delay must be >= minimum request delay.");
		}

		_minimumRequestDelay = minimumRequestDelay;
		_maximumRequestDelay = maximumRequestDelay;
		_utcNowProvider = utcNowProvider ?? throw new ArgumentNullException(nameof(utcNowProvider));
		_delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
		_requestDelaySelector = requestDelaySelector ?? throw new ArgumentNullException(nameof(requestDelaySelector));
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
				TimeSpan selectedDelay = _requestDelaySelector(_minimumRequestDelay, _maximumRequestDelay);
				ValidateSelectedDelay(selectedDelay);
				DateTimeOffset completedAtUtc = _utcNowProvider().ToUniversalTime();
				_nextAllowedStartUtc = completedAtUtc + selectedDelay;
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
		if (_maximumRequestDelay <= TimeSpan.Zero || !_nextAllowedStartUtc.HasValue)
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
	/// Validates one selected delay value produced by the selector callback.
	/// </summary>
	/// <param name="selectedDelay">Selected delay value.</param>
	private void ValidateSelectedDelay(TimeSpan selectedDelay)
	{
		if (selectedDelay < _minimumRequestDelay || selectedDelay > _maximumRequestDelay)
		{
			throw new InvalidOperationException("Selected metadata API request delay is outside the configured bounds.");
		}
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

	/// <summary>
	/// Selects one random delay value between the configured bounds (inclusive).
	/// </summary>
	/// <param name="minimumDelay">Minimum configured delay.</param>
	/// <param name="maximumDelay">Maximum configured delay.</param>
	/// <returns>Selected delay value.</returns>
	private static TimeSpan DefaultSelectDelay(TimeSpan minimumDelay, TimeSpan maximumDelay)
	{
		if (maximumDelay <= minimumDelay)
		{
			return minimumDelay;
		}

		long minimumTicks = minimumDelay.Ticks;
		long rangeTicks = maximumDelay.Ticks - minimumTicks;
		if (rangeTicks == long.MaxValue)
		{
			long selectedOffsetTicks = SelectInclusiveOffsetForFullRange();
			return TimeSpan.FromTicks(minimumTicks + selectedOffsetTicks);
		}

		long randomOffsetTicks = Random.Shared.NextInt64(rangeTicks + 1);
		return TimeSpan.FromTicks(minimumTicks + randomOffsetTicks);
	}

	/// <summary>
	/// Produces one random offset in the inclusive range [0, <see cref="long.MaxValue"/>].
	/// </summary>
	/// <returns>Random offset in ticks.</returns>
	private static long SelectInclusiveOffsetForFullRange()
	{
		Span<byte> randomBytes = stackalloc byte[sizeof(ulong)];
		Random.Shared.NextBytes(randomBytes);
		ulong rawRandomValue = BitConverter.ToUInt64(randomBytes);
		return NormalizeInclusiveOffsetFromRawUInt64(rawRandomValue);
	}

	/// <summary>
	/// Maps one raw random 64-bit value into the inclusive non-negative range used by full-range delay selection.
	/// </summary>
	/// <param name="rawRandomValue">Raw random 64-bit value.</param>
	/// <returns>One non-negative tick offset in the inclusive range [0, <see cref="long.MaxValue"/>].</returns>
	internal static long NormalizeInclusiveOffsetFromRawUInt64(ulong rawRandomValue)
	{
		return (long)(rawRandomValue & long.MaxValue);
	}
}
