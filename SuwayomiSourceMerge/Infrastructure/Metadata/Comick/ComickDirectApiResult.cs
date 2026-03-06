using System.Net;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Represents one direct Comick API request result with typed payload and deterministic classification.
/// </summary>
/// <typeparam name="TPayload">Typed payload model for successful responses.</typeparam>
internal sealed class ComickDirectApiResult<TPayload>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ComickDirectApiResult{TPayload}"/> class.
	/// </summary>
	/// <param name="outcome">Classified request outcome.</param>
	/// <param name="payload">Typed payload when available.</param>
	/// <param name="statusCode">HTTP status code when available.</param>
	/// <param name="diagnostic">Deterministic diagnostic text.</param>
	/// <param name="isCacheOnlyMiss">
	/// Whether this result represents a deterministic cache-only lookup miss (no live request attempted).
	/// </param>
	public ComickDirectApiResult(
		ComickDirectApiOutcome outcome,
		TPayload? payload,
		HttpStatusCode? statusCode,
		string diagnostic,
		bool isCacheOnlyMiss = false)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
		if (isCacheOnlyMiss && outcome != ComickDirectApiOutcome.NotFound)
		{
			throw new ArgumentException(
				"Cache-only miss flag requires NotFound outcome.",
				nameof(isCacheOnlyMiss));
		}

		if (isCacheOnlyMiss && payload is not null)
		{
			throw new ArgumentException(
				"Cache-only miss flag requires a null payload.",
				nameof(isCacheOnlyMiss));
		}

		Outcome = outcome;
		Payload = payload;
		StatusCode = statusCode;
		Diagnostic = diagnostic;
		IsCacheOnlyMiss = isCacheOnlyMiss;
	}

	/// <summary>
	/// Gets the classified request outcome.
	/// </summary>
	public ComickDirectApiOutcome Outcome
	{
		get;
	}

	/// <summary>
	/// Gets the typed payload when available.
	/// </summary>
	public TPayload? Payload
	{
		get;
	}

	/// <summary>
	/// Gets the HTTP status code when available.
	/// </summary>
	public HttpStatusCode? StatusCode
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

	/// <summary>
	/// Gets a value indicating whether this result represents a cache-only lookup miss.
	/// </summary>
	public bool IsCacheOnlyMiss
	{
		get;
	}
}
