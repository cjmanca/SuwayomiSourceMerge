using System.Net;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

/// <summary>
/// Represents one FlareSolverr API request result with extracted upstream response metadata.
/// </summary>
internal sealed class FlaresolverrApiResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FlaresolverrApiResult"/> class.
	/// </summary>
	/// <param name="outcome">Classified request outcome.</param>
	/// <param name="statusCode">FlareSolverr endpoint status code when available.</param>
	/// <param name="upstreamStatusCode">Extracted upstream status code from the wrapper when available.</param>
	/// <param name="upstreamResponseBody">Extracted upstream response body from the wrapper when available.</param>
	/// <param name="diagnostic">Deterministic diagnostic text.</param>
	public FlaresolverrApiResult(
		FlaresolverrApiOutcome outcome,
		HttpStatusCode? statusCode,
		int? upstreamStatusCode,
		string? upstreamResponseBody,
		string diagnostic)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);

		Outcome = outcome;
		StatusCode = statusCode;
		UpstreamStatusCode = upstreamStatusCode;
		UpstreamResponseBody = upstreamResponseBody;
		Diagnostic = diagnostic;
	}

	/// <summary>
	/// Gets the classified request outcome.
	/// </summary>
	public FlaresolverrApiOutcome Outcome
	{
		get;
	}

	/// <summary>
	/// Gets the FlareSolverr endpoint status code when available.
	/// </summary>
	public HttpStatusCode? StatusCode
	{
		get;
	}

	/// <summary>
	/// Gets the extracted upstream status code when available.
	/// </summary>
	public int? UpstreamStatusCode
	{
		get;
	}

	/// <summary>
	/// Gets the extracted upstream response body when available.
	/// </summary>
	public string? UpstreamResponseBody
	{
		get;
	}

	/// <summary>
	/// Gets deterministic diagnostic text.
	/// </summary>
	public string Diagnostic
	{
		get;
	}
}
