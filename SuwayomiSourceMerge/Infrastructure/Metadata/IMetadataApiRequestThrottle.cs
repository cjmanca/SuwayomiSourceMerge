namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Coordinates pacing for outbound metadata API operations.
/// </summary>
internal interface IMetadataApiRequestThrottle
{
	/// <summary>
	/// Executes one asynchronous metadata API operation under the throttle policy.
	/// </summary>
	/// <param name="operation">Operation delegate to execute.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that completes when the operation finishes.</returns>
	Task ExecuteAsync(
		Func<CancellationToken, Task> operation,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Executes one asynchronous metadata API operation under the throttle policy and returns its result.
	/// </summary>
	/// <typeparam name="TResult">Result type returned by the operation.</typeparam>
	/// <param name="operation">Operation delegate to execute.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that completes with the operation result.</returns>
	Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		CancellationToken cancellationToken = default);
}
