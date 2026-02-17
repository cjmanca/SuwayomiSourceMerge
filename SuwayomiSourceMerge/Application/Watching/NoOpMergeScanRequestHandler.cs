using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Application.Watching;

/// <summary>
/// Placeholder merge request handler that reports success without applying mount updates.
/// </summary>
internal sealed class NoOpMergeScanRequestHandler : IMergeScanRequestHandler
{
	/// <summary>Event id emitted for no-op merge dispatch requests.</summary>
	private const string MergeDeferredEvent = "merge.dispatch.deferred";

	/// <summary>
	/// Logger dependency.
	/// </summary>
	private readonly ISsmLogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="NoOpMergeScanRequestHandler"/> class.
	/// </summary>
	/// <param name="logger">Logger dependency.</param>
	public NoOpMergeScanRequestHandler(ISsmLogger logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public MergeScanDispatchOutcome DispatchMergeScan(
		string reason,
		bool force,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);
		cancellationToken.ThrowIfCancellationRequested();

		_logger.Debug(
			MergeDeferredEvent,
			"Merge dispatch is currently deferred by no-op handler.",
			BuildContext(
				("reason", reason),
				("force", force ? "true" : "false")));
		return MergeScanDispatchOutcome.Success;
	}

	/// <summary>
	/// Builds one immutable logging context dictionary.
	/// </summary>
	/// <param name="pairs">Context key/value pairs.</param>
	/// <returns>Context dictionary.</returns>
	private static IReadOnlyDictionary<string, string> BuildContext(params (string Key, string Value)[] pairs)
	{
		Dictionary<string, string> context = new(StringComparer.Ordinal);
		for (int index = 0; index < pairs.Length; index++)
		{
			(string key, string value) = pairs[index];
			if (!string.IsNullOrWhiteSpace(key))
			{
				context[key] = value;
			}
		}

		return context;
	}
}
