namespace SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

/// <summary>
/// Classifies one FlareSolverr API request outcome.
/// </summary>
internal enum FlaresolverrApiOutcome
{
	/// <summary>
	/// Request completed successfully and wrapper extraction succeeded.
	/// </summary>
	Success = 0,

	/// <summary>
	/// Request completed with a non-success HTTP status code.
	/// </summary>
	HttpFailure = 1,

	/// <summary>
	/// Request failed before receiving a usable response.
	/// </summary>
	TransportFailure = 2,

	/// <summary>
	/// Request was canceled by the caller.
	/// </summary>
	Cancelled = 3,

	/// <summary>
	/// Response content could not be parsed into the expected wrapper shape.
	/// </summary>
	MalformedPayload = 4
}
