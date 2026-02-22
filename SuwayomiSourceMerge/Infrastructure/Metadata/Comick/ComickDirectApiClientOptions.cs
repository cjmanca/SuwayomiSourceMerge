namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Carries direct Comick API client endpoint and timeout settings.
/// </summary>
internal sealed class ComickDirectApiClientOptions
{
	/// <summary>
	/// Default direct Comick API base URI.
	/// </summary>
	public const string DefaultBaseUri = "https://api.comick.dev";

	/// <summary>
	/// Default direct Comick request timeout in seconds.
	/// </summary>
	public const int DefaultTimeoutSeconds = 30;

	/// <summary>
	/// Initializes a new instance of the <see cref="ComickDirectApiClientOptions"/> class with default settings.
	/// </summary>
	public ComickDirectApiClientOptions()
		: this(
			new Uri(DefaultBaseUri, UriKind.Absolute),
			TimeSpan.FromSeconds(DefaultTimeoutSeconds))
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ComickDirectApiClientOptions"/> class.
	/// </summary>
	/// <param name="baseUri">Comick API base URI.</param>
	/// <param name="requestTimeout">HTTP request timeout.</param>
	public ComickDirectApiClientOptions(Uri baseUri, TimeSpan requestTimeout)
	{
		ArgumentNullException.ThrowIfNull(baseUri);

		if (!baseUri.IsAbsoluteUri)
		{
			throw new ArgumentException(
				"Comick API base URI must be absolute.",
				nameof(baseUri));
		}

		if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException(
				"Comick API base URI must use http or https.",
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
	/// Gets the normalized absolute Comick API base URI.
	/// </summary>
	public Uri BaseUri
	{
		get;
	}

	/// <summary>
	/// Gets the request timeout used by the direct API client.
	/// </summary>
	public TimeSpan RequestTimeout
	{
		get;
	}
}
