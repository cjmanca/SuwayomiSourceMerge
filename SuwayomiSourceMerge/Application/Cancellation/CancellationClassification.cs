namespace SuwayomiSourceMerge.Application.Cancellation;

/// <summary>
/// Classifies cancellation exceptions as cooperative or non-cooperative relative to a caller token.
/// </summary>
internal static class CancellationClassification
{
	/// <summary>
	/// Returns whether the exception represents cooperative cancellation for the supplied caller token.
	/// </summary>
	/// <param name="exception">Cancellation exception instance.</param>
	/// <param name="callerToken">Caller-owned cancellation token.</param>
	/// <returns><see langword="true"/> when cancellation is cooperative; otherwise <see langword="false"/>.</returns>
	public static bool IsCooperative(OperationCanceledException exception, CancellationToken callerToken)
	{
		ArgumentNullException.ThrowIfNull(exception);

		if (!callerToken.IsCancellationRequested)
		{
			return false;
		}

		if (!exception.CancellationToken.CanBeCanceled)
		{
			return true;
		}

		return exception.CancellationToken == callerToken;
	}
}
