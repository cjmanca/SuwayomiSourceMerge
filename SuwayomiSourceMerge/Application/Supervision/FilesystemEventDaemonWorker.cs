using SuwayomiSourceMerge.Application.Cancellation;
using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Runs the filesystem event trigger pipeline continuously for daemon operation.
/// </summary>
internal sealed class FilesystemEventDaemonWorker : IDaemonWorker
{
	/// <summary>Event id emitted when the worker loop starts.</summary>
	private const string WorkerStartedEvent = "supervisor.worker.started";

	/// <summary>Event id emitted when the worker loop stops.</summary>
	private const string WorkerStoppedEvent = "supervisor.worker.stopped";

	/// <summary>Event id emitted when merge runtime lifecycle cleanup throws.</summary>
	private const string WorkerLifecycleWarningEvent = "supervisor.worker.lifecycle_warning";

	/// <summary>
	/// Pipeline used to execute one trigger tick.
	/// </summary>
	private readonly FilesystemEventTriggerPipeline _triggerPipeline;

	/// <summary>
	/// Logger used for worker lifecycle diagnostics.
	/// </summary>
	private readonly ISsmLogger _logger;

	/// <summary>
	/// Merge runtime lifecycle dependency.
	/// </summary>
	private readonly IMergeRuntimeLifecycle _mergeRuntimeLifecycle;

	/// <summary>
	/// Initializes a new instance of the <see cref="FilesystemEventDaemonWorker"/> class.
	/// </summary>
	/// <param name="triggerPipeline">Trigger pipeline dependency.</param>
	/// <param name="mergeRuntimeLifecycle">Merge runtime lifecycle dependency.</param>
	/// <param name="logger">Logger dependency.</param>
	public FilesystemEventDaemonWorker(
		FilesystemEventTriggerPipeline triggerPipeline,
		IMergeRuntimeLifecycle mergeRuntimeLifecycle,
		ISsmLogger logger)
	{
		_triggerPipeline = triggerPipeline ?? throw new ArgumentNullException(nameof(triggerPipeline));
		_mergeRuntimeLifecycle = mergeRuntimeLifecycle ?? throw new ArgumentNullException(nameof(mergeRuntimeLifecycle));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task RunAsync(CancellationToken cancellationToken, CancellationToken shutdownCancellationToken = default)
	{
		return Task.Factory.StartNew(
			RunWorkerLoop,
			CancellationToken.None,
			TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
			TaskScheduler.Default);

		void RunWorkerLoop()
		{
			_logger.Debug(WorkerStartedEvent, "Filesystem event daemon worker started.");

			try
			{
				_mergeRuntimeLifecycle.OnWorkerStarting(cancellationToken);
				while (true)
				{
					cancellationToken.ThrowIfCancellationRequested();
					_triggerPipeline.Tick(DateTimeOffset.UtcNow, cancellationToken);
				}
			}
			finally
			{
				try
				{
					_mergeRuntimeLifecycle.OnWorkerStopping(shutdownCancellationToken);
				}
				catch (OperationCanceledException exception) when (CancellationClassification.IsCooperative(exception, shutdownCancellationToken))
				{
					_logger.Debug(
						WorkerStoppedEvent,
						"Merge runtime shutdown lifecycle hook observed cooperative cancellation.");
				}
				catch (Exception exception)
				{
					_logger.Warning(
						WorkerLifecycleWarningEvent,
						"Merge runtime shutdown lifecycle hook threw a non-fatal exception.",
						BuildContext(
							("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
							("message", exception.Message)));
				}

				try
				{
					_triggerPipeline.Dispose();
				}
				catch (Exception exception)
				{
					_logger.Warning(
						WorkerLifecycleWarningEvent,
						"Filesystem trigger pipeline dispose threw a non-fatal exception.",
						BuildContext(
							("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
							("message", exception.Message)));
				}

				_logger.Debug(WorkerStoppedEvent, "Filesystem event daemon worker stopped.");
			}
		}
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
