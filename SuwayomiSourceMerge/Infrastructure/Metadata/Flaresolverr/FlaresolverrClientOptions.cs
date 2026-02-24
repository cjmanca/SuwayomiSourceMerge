namespace SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

/// <summary>
/// Carries FlareSolverr API endpoint and timeout settings.
/// </summary>
internal sealed class FlaresolverrClientOptions
{
	/// <summary>
	/// Default FlareSolverr request timeout in seconds.
	/// </summary>
	public const int DefaultTimeoutSeconds = 30;

	/// <summary>
	/// Initializes a new instance of the <see cref="FlaresolverrClientOptions"/> class.
	/// </summary>
	/// <param name="baseUri">FlareSolverr server base URI.</param>
	public FlaresolverrClientOptions(Uri baseUri)
		: this(baseUri, TimeSpan.FromSeconds(DefaultTimeoutSeconds))
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FlaresolverrClientOptions"/> class.
	/// </summary>
	/// <param name="baseUri">FlareSolverr server base URI.</param>
	/// <param name="requestTimeout">HTTP request timeout.</param>
	public FlaresolverrClientOptions(Uri baseUri, TimeSpan requestTimeout)
	{
		ArgumentNullException.ThrowIfNull(baseUri);

		if (!baseUri.IsAbsoluteUri)
		{
			throw new ArgumentException(
				"FlareSolverr server base URI must be absolute.",
				nameof(baseUri));
		}

		if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException(
				"FlareSolverr server base URI must use http or https.",
				nameof(baseUri));
		}

		if (requestTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(requestTimeout),
				requestTimeout,
				"Request timeout must be > 0.");
		}

		BaseUri = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
		RequestTimeout = requestTimeout;
	}

	/// <summary>
	/// Gets the normalized absolute FlareSolverr server base URI.
	/// </summary>
	public Uri BaseUri
	{
		get;
	}

	/// <summary>
	/// Gets the request timeout used by the FlareSolverr client.
	/// </summary>
	public TimeSpan RequestTimeout
	{
		get;
	}
}
