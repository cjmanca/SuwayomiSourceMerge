namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Executes metadata API operations immediately without introducing pacing delays.
/// </summary>
internal sealed class NoOpMetadataApiRequestThrottle : IMetadataApiRequestThrottle
{
	/// <inheritdoc />
	public Task ExecuteAsync(
		Func<CancellationToken, Task> operation,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(operation);
		cancellationToken.ThrowIfCancellationRequested();
		return operation(cancellationToken);
	}

	/// <inheritdoc />
	public Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(operation);
		cancellationToken.ThrowIfCancellationRequested();
		return operation(cancellationToken);
	}
}
