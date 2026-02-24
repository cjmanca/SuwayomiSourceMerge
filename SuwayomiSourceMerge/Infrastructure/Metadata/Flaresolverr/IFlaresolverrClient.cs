namespace SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

/// <summary>
/// Defines FlareSolverr API access behavior for generic wrapper requests.
/// </summary>
internal interface IFlaresolverrClient
{
	/// <summary>
	/// Posts one caller-provided JSON payload to the FlareSolverr <c>/v1</c> endpoint.
	/// </summary>
	/// <param name="requestPayloadJson">Raw JSON payload posted unchanged.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Classified request result including extracted upstream response metadata when successful.</returns>
	Task<FlaresolverrApiResult> PostV1Async(
		string requestPayloadJson,
		CancellationToken cancellationToken = default);
}
