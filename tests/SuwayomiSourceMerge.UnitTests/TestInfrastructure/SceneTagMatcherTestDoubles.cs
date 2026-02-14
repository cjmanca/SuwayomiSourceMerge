namespace SuwayomiSourceMerge.UnitTests.TestInfrastructure;

using SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Counts matcher invocations while delegating matching behavior to <see cref="SceneTagMatcher" />.
/// </summary>
internal sealed class CountingSceneTagMatcher : ISceneTagMatcher
{
	/// <summary>
	/// Inner matcher used for deterministic configured-tag matching behavior.
	/// </summary>
	private readonly ISceneTagMatcher _innerMatcher;

	/// <summary>
	/// Initializes a new instance of the <see cref="CountingSceneTagMatcher" /> class.
	/// </summary>
	/// <param name="configuredTags">Configured scene tags used by the inner matcher.</param>
	public CountingSceneTagMatcher(IEnumerable<string> configuredTags)
	{
		_innerMatcher = new SceneTagMatcher(configuredTags);
	}

	/// <summary>
	/// Gets the total number of matcher invocations.
	/// </summary>
	public int MatchCallCount
	{
		get;
		private set;
	}

	/// <inheritdoc />
	public bool IsMatch(string candidate)
	{
		MatchCallCount++;
		return _innerMatcher.IsMatch(candidate);
	}
}

/// <summary>
/// Alternates between true and false results on each invocation.
/// </summary>
/// <remarks>
/// This test double is used to verify cache behavior when matcher determinism assumptions are violated.
/// </remarks>
internal sealed class FlippingSceneTagMatcher : ISceneTagMatcher
{
	/// <summary>
	/// Result to return on the next invocation.
	/// </summary>
	private bool _nextResult;

	/// <summary>
	/// Initializes a new instance of the <see cref="FlippingSceneTagMatcher" /> class.
	/// </summary>
	/// <param name="initialResult">First result returned by <see cref="IsMatch" />.</param>
	public FlippingSceneTagMatcher(bool initialResult)
	{
		_nextResult = initialResult;
	}

	/// <summary>
	/// Gets the total number of matcher invocations.
	/// </summary>
	public int MatchCallCount
	{
		get;
		private set;
	}

	/// <inheritdoc />
	public bool IsMatch(string candidate)
	{
		MatchCallCount++;
		bool current = _nextResult;
		_nextResult = !current;
		return current;
	}
}

/// <summary>
/// Throws <see cref="InvalidOperationException" /> for every matcher invocation.
/// </summary>
internal sealed class ThrowingSceneTagMatcher : ISceneTagMatcher
{
	/// <summary>
	/// Error message returned by thrown exceptions.
	/// </summary>
	private readonly string _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="ThrowingSceneTagMatcher" /> class.
	/// </summary>
	/// <param name="message">Exception message used by <see cref="IsMatch" />.</param>
	public ThrowingSceneTagMatcher(string message = "matcher-failure")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(message);
		_message = message;
	}

	/// <inheritdoc />
	public bool IsMatch(string candidate)
	{
		throw new InvalidOperationException(_message);
	}
}
