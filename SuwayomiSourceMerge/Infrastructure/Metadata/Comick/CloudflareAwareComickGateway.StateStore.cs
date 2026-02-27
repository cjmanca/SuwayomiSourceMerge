namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Metadata state-store helper behavior for <see cref="CloudflareAwareComickGateway"/>.
/// </summary>
internal sealed partial class CloudflareAwareComickGateway
{
	/// <summary>
	/// Reads metadata state snapshot with best-effort fallback semantics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI associated with the state operation.</param>
	/// <param name="operation">Operation identifier used for diagnostics.</param>
	/// <returns>Current state snapshot, or <see cref="MetadataStateSnapshot.Empty"/> on non-fatal read failure.</returns>
	private MetadataStateSnapshot TryReadMetadataStateSnapshot(Uri endpointUri, string operation)
	{
		ArgumentNullException.ThrowIfNull(endpointUri);
		ArgumentException.ThrowIfNullOrWhiteSpace(operation);

		try
		{
			return _metadataStateStore.Read();
		}
		catch (Exception exception) when (!IsFatalException(exception))
		{
			LogStateStoreOperationFailed(endpointUri, operation, exception);
			return MetadataStateSnapshot.Empty;
		}
	}

	/// <summary>
	/// Applies one metadata state transform with best-effort fallback semantics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI associated with the state operation.</param>
	/// <param name="operation">Operation identifier used for diagnostics.</param>
	/// <param name="transformer">State transform callback.</param>
	/// <returns><see langword="true"/> when the transform succeeds; otherwise <see langword="false"/>.</returns>
	private bool TryTransformMetadataStateSnapshot(
		Uri endpointUri,
		string operation,
		Func<MetadataStateSnapshot, MetadataStateSnapshot> transformer)
	{
		ArgumentNullException.ThrowIfNull(endpointUri);
		ArgumentException.ThrowIfNullOrWhiteSpace(operation);
		ArgumentNullException.ThrowIfNull(transformer);

		try
		{
			_metadataStateStore.Transform(transformer);
			return true;
		}
		catch (Exception exception) when (!IsFatalException(exception))
		{
			LogStateStoreOperationFailed(endpointUri, operation, exception);
			return false;
		}
	}

	/// <summary>
	/// Determines whether an exception should be treated as fatal and rethrown.
	/// </summary>
	/// <param name="exception">Exception to classify.</param>
	/// <returns><see langword="true"/> when fatal; otherwise <see langword="false"/>.</returns>
	private static bool IsFatalException(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return exception is OutOfMemoryException
			or StackOverflowException
			or AccessViolationException;
	}
}
