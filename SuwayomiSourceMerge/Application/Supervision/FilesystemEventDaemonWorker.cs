using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Runs the filesystem event trigger pipeline continuously for daemon operation.
/// </summary>
internal sealed class FilesystemEventDaemonWorker : IDaemonWorker
{
	/// <summary>Event id emitted when the worker loop starts.</summary>
	private const string WORKER_STARTED_EVENT = "supervisor.worker.started";

	/// <summary>Event id emitted when the worker loop stops.</summary>
	private const string WORKER_STOPPED_EVENT = "supervisor.worker.stopped";

	/// <summary>
	/// Pipeline used to execute one trigger tick.
	/// </summary>
	private readonly FilesystemEventTriggerPipeline _triggerPipeline;

	/// <summary>
	/// Logger used for worker lifecycle diagnostics.
	/// </summary>
	private readonly ISsmLogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="FilesystemEventDaemonWorker"/> class.
	/// </summary>
	/// <param name="triggerPipeline">Trigger pipeline dependency.</param>
	/// <param name="logger">Logger dependency.</param>
	public FilesystemEventDaemonWorker(
		FilesystemEventTriggerPipeline triggerPipeline,
		ISsmLogger logger)
	{
		_triggerPipeline = triggerPipeline ?? throw new ArgumentNullException(nameof(triggerPipeline));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task RunAsync(CancellationToken cancellationToken)
	{
		return Task.Run(
			() =>
			{
				_logger.Debug(WORKER_STARTED_EVENT, "Filesystem event daemon worker started.");

				try
				{
					while (true)
					{
						cancellationToken.ThrowIfCancellationRequested();
						_triggerPipeline.Tick(DateTimeOffset.UtcNow, cancellationToken);
					}
				}
				finally
				{
					_logger.Debug(WORKER_STOPPED_EVENT, "Filesystem event daemon worker stopped.");
				}
			},
			CancellationToken.None);
	}
}
