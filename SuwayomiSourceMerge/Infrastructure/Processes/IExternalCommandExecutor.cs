namespace SuwayomiSourceMerge.Infrastructure.Processes;

/// <summary>
/// Executes external commands with deterministic timeout, cancellation, and output-capture behavior.
/// </summary>
internal interface IExternalCommandExecutor
{
	/// <summary>
	/// Executes an external command according to the provided request.
	/// </summary>
	/// <param name="request">Command request containing executable, arguments, and execution limits.</param>
	/// <param name="cancellationToken">Cancellation token used to stop execution early.</param>
	/// <returns>
	/// A typed result describing command outcome, captured output, and elapsed execution duration.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown when the request contains invalid arguments.</exception>
	ExternalCommandResult Execute(ExternalCommandRequest request, CancellationToken cancellationToken = default);
}
