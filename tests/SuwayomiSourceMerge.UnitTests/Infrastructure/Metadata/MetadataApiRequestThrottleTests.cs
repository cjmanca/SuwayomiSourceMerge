namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="MetadataApiRequestThrottle"/>.
/// </summary>
public sealed class MetadataApiRequestThrottleTests
{
	/// <summary>
	/// Verifies configured min/max pacing selects one delay per operation from the allowed range.
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_Expected_ShouldApplySelectedDelayFromConfiguredRange()
	{
		DeterministicClock clock = new(new DateTimeOffset(2026, 2, 28, 11, 0, 0, TimeSpan.Zero));
		Queue<TimeSpan> selectedDelays = new([TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5)]);
		MetadataApiRequestThrottle throttle = new(
			TimeSpan.FromSeconds(3),
			TimeSpan.FromSeconds(7),
			clock.GetUtcNow,
			clock.DelayAsync,
			(minimumDelay, maximumDelay) => selectedDelays.Dequeue());

		await throttle.ExecuteAsync(
			token =>
			{
				clock.Advance(TimeSpan.FromSeconds(1));
				return Task.CompletedTask;
			});
		await throttle.ExecuteAsync(
			token =>
			{
				clock.Advance(TimeSpan.FromSeconds(1));
				return Task.CompletedTask;
			});
		await throttle.ExecuteAsync(
			token =>
			{
				clock.Advance(TimeSpan.FromSeconds(1));
				return Task.CompletedTask;
			});

		Assert.Equal([TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7)], clock.DelayRequests);
	}

	/// <summary>
	/// Verifies completion-anchored pacing delays the next operation start until the configured delay elapses.
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_Expected_ShouldApplyCompletionAnchoredDelayBetweenOperations()
	{
		DeterministicClock clock = new(new DateTimeOffset(2026, 2, 28, 12, 0, 0, TimeSpan.Zero));
		MetadataApiRequestThrottle throttle = new(
			TimeSpan.FromSeconds(5),
			clock.GetUtcNow,
			clock.DelayAsync);

		DateTimeOffset firstStartUtc = DateTimeOffset.MinValue;
		DateTimeOffset secondStartUtc = DateTimeOffset.MinValue;

		int firstResult = await throttle.ExecuteAsync(
			token =>
			{
				firstStartUtc = clock.GetUtcNow();
				clock.Advance(TimeSpan.FromSeconds(2));
				return Task.FromResult(11);
			});
		int secondResult = await throttle.ExecuteAsync(
			token =>
			{
				secondStartUtc = clock.GetUtcNow();
				clock.Advance(TimeSpan.FromSeconds(1));
				return Task.FromResult(22);
			});

		Assert.Equal(11, firstResult);
		Assert.Equal(22, secondResult);
		Assert.Equal(new DateTimeOffset(2026, 2, 28, 12, 0, 0, TimeSpan.Zero), firstStartUtc);
		Assert.Equal(new DateTimeOffset(2026, 2, 28, 12, 0, 7, TimeSpan.Zero), secondStartUtc);
		Assert.Single(clock.DelayRequests);
		Assert.Equal(TimeSpan.FromSeconds(5), clock.DelayRequests[0]);
	}

	/// <summary>
	/// Verifies zero-delay configuration bypasses pacing waits.
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_Edge_ShouldSkipPacingWait_WhenDelayIsZero()
	{
		DeterministicClock clock = new(new DateTimeOffset(2026, 2, 28, 12, 30, 0, TimeSpan.Zero));
		MetadataApiRequestThrottle throttle = new(
			TimeSpan.Zero,
			clock.GetUtcNow,
			clock.DelayAsync);

		await throttle.ExecuteAsync(
			token =>
			{
				clock.Advance(TimeSpan.FromSeconds(1));
				return Task.CompletedTask;
			});
		await throttle.ExecuteAsync(
			token =>
			{
				clock.Advance(TimeSpan.FromSeconds(1));
				return Task.CompletedTask;
			});

		Assert.Empty(clock.DelayRequests);
	}

	/// <summary>
	/// Verifies concurrent submissions are serialized so a second operation cannot begin until the first operation finishes.
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_Expected_ShouldSerializeConcurrentOperations()
	{
		MetadataApiRequestThrottle throttle = new(
			TimeSpan.Zero,
			static () => DateTimeOffset.UtcNow,
			static (delay, cancellationToken) => Task.CompletedTask);

		TaskCompletionSource<object?> firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource<object?> releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
		List<int> executionMarkers = [];

		Task firstTask = throttle.ExecuteAsync(
			async token =>
			{
				executionMarkers.Add(1);
				firstStarted.SetResult(null);
				await releaseFirst.Task;
				executionMarkers.Add(2);
			});

		await firstStarted.Task;

		bool secondStarted = false;
		TaskCompletionSource<object?> secondEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
		Task secondTask = throttle.ExecuteAsync(
			token =>
			{
				secondStarted = true;
				secondEntered.SetResult(null);
				executionMarkers.Add(3);
				return Task.CompletedTask;
			});

		Assert.False(secondStarted);
		Assert.False(secondEntered.Task.IsCompleted);
		Assert.False(secondTask.IsCompleted);

		releaseFirst.SetResult(null);
		await Task.WhenAll(firstTask, secondTask);

		Assert.True(secondStarted);
		Assert.Equal([1, 2, 3], executionMarkers);
	}

	/// <summary>
	/// Verifies cancellation during an active pacing wait is propagated and does not execute the operation.
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_Edge_ShouldThrowOperationCanceledException_WhenCancellationOccursDuringPacingDelay()
	{
		DateTimeOffset currentUtc = new(2026, 2, 28, 13, 0, 0, TimeSpan.Zero);
		int delayCallCount = 0;
		using CancellationTokenSource cancellationTokenSource = new();
		MetadataApiRequestThrottle throttle = new(
			TimeSpan.FromSeconds(5),
			() => currentUtc,
			(delay, cancellationToken) =>
			{
				delayCallCount++;
				cancellationTokenSource.Cancel();
				cancellationToken.ThrowIfCancellationRequested();
				return Task.CompletedTask;
			});

		await throttle.ExecuteAsync(token => Task.CompletedTask, CancellationToken.None);

		bool secondOperationInvoked = false;
		await Assert.ThrowsAsync<OperationCanceledException>(
			() => throttle.ExecuteAsync(
				token =>
				{
					secondOperationInvoked = true;
					return Task.CompletedTask;
				},
				cancellationTokenSource.Token));

		Assert.Equal(1, delayCallCount);
		Assert.False(secondOperationInvoked);
	}

	/// <summary>
	/// Verifies failed operations still advance the pacing window for subsequent operations.
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_Failure_ShouldAdvancePacingWindow_WhenOperationThrows()
	{
		DeterministicClock clock = new(new DateTimeOffset(2026, 2, 28, 13, 30, 0, TimeSpan.Zero));
		MetadataApiRequestThrottle throttle = new(
			TimeSpan.FromSeconds(4),
			clock.GetUtcNow,
			clock.DelayAsync);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => throttle.ExecuteAsync(
				token =>
				{
					clock.Advance(TimeSpan.FromSeconds(2));
					throw new InvalidOperationException("simulated failure");
				}));

		DateTimeOffset secondStartUtc = DateTimeOffset.MinValue;
		await throttle.ExecuteAsync(
			token =>
			{
				secondStartUtc = clock.GetUtcNow();
				return Task.CompletedTask;
			});

		Assert.Equal(new DateTimeOffset(2026, 2, 28, 13, 30, 6, TimeSpan.Zero), secondStartUtc);
		Assert.Single(clock.DelayRequests);
		Assert.Equal(TimeSpan.FromSeconds(4), clock.DelayRequests[0]);
	}

	/// <summary>
	/// Verifies constructor and delegate guards reject invalid inputs.
	/// </summary>
	[Fact]
	public async Task ConstructorAndExecuteAsync_Failure_ShouldThrow_WhenInputsInvalid()
	{
		ArgumentOutOfRangeException delayException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new MetadataApiRequestThrottle(TimeSpan.FromMilliseconds(-1)));
		Assert.Equal("minimumRequestDelay", delayException.ParamName);
		ArgumentOutOfRangeException invalidRangeException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new MetadataApiRequestThrottle(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
		Assert.Equal("maximumRequestDelay", invalidRangeException.ParamName);

		Assert.Throws<ArgumentNullException>(
			() => new MetadataApiRequestThrottle(
				TimeSpan.FromSeconds(1),
				null!,
				static (delay, cancellationToken) => Task.CompletedTask));
		Assert.Throws<ArgumentNullException>(
			() => new MetadataApiRequestThrottle(
				TimeSpan.FromSeconds(1),
				static () => DateTimeOffset.UtcNow,
				null!));
		Assert.Throws<ArgumentNullException>(
			() => new MetadataApiRequestThrottle(
				TimeSpan.FromSeconds(1),
				TimeSpan.FromSeconds(2),
				static () => DateTimeOffset.UtcNow,
				static (delay, cancellationToken) => Task.CompletedTask,
				null!));

		MetadataApiRequestThrottle throttle = new(
			TimeSpan.Zero,
			static () => DateTimeOffset.UtcNow,
			static (delay, cancellationToken) => Task.CompletedTask);

		await Assert.ThrowsAsync<ArgumentNullException>(() => throttle.ExecuteAsync((Func<CancellationToken, Task>)null!));
		await Assert.ThrowsAsync<ArgumentNullException>(() => throttle.ExecuteAsync<int>((Func<CancellationToken, Task<int>>)null!));
	}

	/// <summary>
	/// Verifies full-range random raw-bit normalization preserves inclusive non-negative bounds.
	/// </summary>
	/// <param name="rawRandomValue">Raw 64-bit random value.</param>
	/// <param name="expectedOffsetTicks">Expected normalized non-negative tick offset.</param>
	[Theory]
	[InlineData(0UL, 0L)]
	[InlineData(1UL, 1L)]
	[InlineData(9223372036854775807UL, long.MaxValue)]
	[InlineData(9223372036854775808UL, 0L)]
	[InlineData(18446744073709551615UL, long.MaxValue)]
	public void NormalizeInclusiveOffsetFromRawUInt64_Expected_ShouldMapRawBitsIntoInclusiveRange(
		ulong rawRandomValue,
		long expectedOffsetTicks)
	{
		long selectedOffsetTicks = MetadataApiRequestThrottle.NormalizeInclusiveOffsetFromRawUInt64(rawRandomValue);

		Assert.Equal(expectedOffsetTicks, selectedOffsetTicks);
		Assert.InRange(selectedOffsetTicks, 0L, long.MaxValue);
	}

	/// <summary>
	/// Deterministic clock/delay fixture for throttle tests.
	/// </summary>
	private sealed class DeterministicClock
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DeterministicClock"/> class.
		/// </summary>
		/// <param name="initialUtc">Initial UTC timestamp.</param>
		public DeterministicClock(DateTimeOffset initialUtc)
		{
			CurrentUtc = initialUtc.ToUniversalTime();
		}

		/// <summary>
		/// Gets the captured delay requests.
		/// </summary>
		public List<TimeSpan> DelayRequests
		{
			get;
		} = [];

		/// <summary>
		/// Gets the current deterministic UTC timestamp.
		/// </summary>
		public DateTimeOffset CurrentUtc
		{
			get;
			private set;
		}

		/// <summary>
		/// Returns the current deterministic UTC timestamp.
		/// </summary>
		/// <returns>Current UTC timestamp.</returns>
		public DateTimeOffset GetUtcNow()
		{
			return CurrentUtc;
		}

		/// <summary>
		/// Advances the deterministic UTC timestamp by the specified offset.
		/// </summary>
		/// <param name="offset">Offset to apply.</param>
		public void Advance(TimeSpan offset)
		{
			CurrentUtc += offset;
		}

		/// <summary>
		/// Applies one deterministic delay by recording and advancing the current timestamp.
		/// </summary>
		/// <param name="delay">Delay requested by the throttle.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>A completed task.</returns>
		public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			DelayRequests.Add(delay);
			CurrentUtc += delay;
			return Task.CompletedTask;
		}
	}
}
