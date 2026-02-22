namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Classifies one direct Comick API request outcome.
/// </summary>
internal enum ComickDirectApiOutcome
{
	/// <summary>
	/// Request completed successfully and payload parsing succeeded.
	/// </summary>
	Success = 0,

	/// <summary>
	/// Request was blocked by a Cloudflare challenge.
	/// </summary>
	CloudflareBlocked = 1,

	/// <summary>
	/// Requested resource was not found.
	/// </summary>
	NotFound = 2,

	/// <summary>
	/// Request completed with a non-success HTTP status code that was not classified as a Cloudflare block.
	/// </summary>
	HttpFailure = 3,

	/// <summary>
	/// Request failed before receiving an HTTP response.
	/// </summary>
	TransportFailure = 4,

	/// <summary>
	/// Request was canceled by the caller.
	/// </summary>
	Cancelled = 5,

	/// <summary>
	/// Response content could not be parsed into the expected typed model.
	/// </summary>
	MalformedPayload = 6
}
