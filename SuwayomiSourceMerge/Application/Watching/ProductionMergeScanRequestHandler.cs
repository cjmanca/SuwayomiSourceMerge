using SuwayomiSourceMerge.Application.Cancellation;
using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Application.Watching;

/// <summary>
/// Production merge dispatch handler that runs real mount workflow orchestration.
/// </summary>
internal sealed class ProductionMergeScanRequestHandler : IMergeScanRequestHandler
{
	/// <summary>Event id emitted when merge dispatch starts.</summary>
	private const string MergeDispatchStartedEvent = "merge.dispatch.started";

	/// <summary>Event id emitted when merge dispatch completes successfully.</summary>
	private const string MergeDispatchCompletedEvent = "merge.dispatch.completed";

	/// <summary>Event id emitted when merge dispatch is busy and will be retried.</summary>
	private const string MergeDispatchBusyEvent = "merge.dispatch.busy";

	/// <summary>Event id emitted when merge dispatch reports mixed busy/failure outcomes.</summary>
	private const string MergeDispatchMixedEvent = "merge.dispatch.mixed";

	/// <summary>Event id emitted when merge dispatch fails.</summary>
	private const string MergeDispatchFailedEvent = "merge.dispatch.failed";

	/// <summary>
	/// Merge workflow dependency.
	/// </summary>
	private readonly IMergeMountWorkflow _mergeMountWorkflow;

	/// <summary>
	/// Logger dependency.
	/// </summary>
	private readonly ISsmLogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProductionMergeScanRequestHandler"/> class.
	/// </summary>
	/// <param name="mergeMountWorkflow">Merge workflow dependency.</param>
	/// <param name="logger">Logger dependency.</param>
	public ProductionMergeScanRequestHandler(IMergeMountWorkflow mergeMountWorkflow, ISsmLogger logger)
	{
		_mergeMountWorkflow = mergeMountWorkflow ?? throw new ArgumentNullException(nameof(mergeMountWorkflow));
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

		_logger.Normal(
			MergeDispatchStartedEvent,
			"Starting production merge dispatch.",
			BuildContext(
				("reason", reason),
				("force", force ? "true" : "false")));

		MergeScanDispatchOutcome outcome;
		try
		{
			outcome = _mergeMountWorkflow.RunMergePass(reason, force, cancellationToken);
		}
		catch (OperationCanceledException exception) when (CancellationClassification.IsCooperative(exception, cancellationToken))
		{
			throw;
		}
		catch (OperationCanceledException exception)
		{
			_logger.Error(
				MergeDispatchFailedEvent,
				"Production merge dispatch threw a non-cooperative cancellation exception and was mapped to failure outcome.",
				BuildContext(
					("reason", reason),
					("force", force ? "true" : "false"),
					("outcome", MergeScanDispatchOutcome.Failure.ToString()),
					("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
					("message", exception.Message)));
			return MergeScanDispatchOutcome.Failure;
		}
		catch (Exception exception)
		{
			_logger.Error(
				MergeDispatchFailedEvent,
				"Production merge dispatch threw an unhandled exception and was mapped to failure outcome.",
				BuildContext(
					("reason", reason),
					("force", force ? "true" : "false"),
					("outcome", MergeScanDispatchOutcome.Failure.ToString()),
					("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
					("message", exception.Message)));
			return MergeScanDispatchOutcome.Failure;
		}

		if (outcome == MergeScanDispatchOutcome.Success)
		{
			_logger.Normal(
				MergeDispatchCompletedEvent,
				"Production merge dispatch completed successfully.",
				BuildContext(
					("reason", reason),
					("force", force ? "true" : "false")));
			return outcome;
		}

		if (outcome == MergeScanDispatchOutcome.Busy)
		{
			_logger.Warning(
				MergeDispatchBusyEvent,
				"Production merge dispatch reported busy outcome.",
				BuildContext(
					("reason", reason),
					("force", force ? "true" : "false"),
					("outcome", outcome.ToString())));
			return outcome;
		}

		if (outcome == MergeScanDispatchOutcome.Mixed)
		{
			_logger.Warning(
				MergeDispatchMixedEvent,
				"Production merge dispatch reported mixed busy/failure outcome.",
				BuildContext(
					("reason", reason),
					("force", force ? "true" : "false"),
					("outcome", outcome.ToString())));
			return outcome;
		}

		if (outcome == MergeScanDispatchOutcome.Failure)
		{
			_logger.Error(
				MergeDispatchFailedEvent,
				"Production merge dispatch reported failure outcome.",
				BuildContext(
					("reason", reason),
					("force", force ? "true" : "false"),
					("outcome", outcome.ToString())));
			return MergeScanDispatchOutcome.Failure;
		}

		_logger.Error(
			MergeDispatchFailedEvent,
			"Production merge dispatch reported unexpected outcome and it was normalized to failure.",
			BuildContext(
				("reason", reason),
				("force", force ? "true" : "false"),
				("outcome", outcome.ToString())));
		return MergeScanDispatchOutcome.Failure;
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
