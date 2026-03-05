using SuwayomiSourceMerge.Infrastructure.Metadata;

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
	/// Default relative path for Comick search requests.
	/// </summary>
	public const string DefaultSearchEndpointPath = "v1.0/search/";

	/// <summary>
	/// Default relative path prefix for Comick comic-detail requests.
	/// </summary>
	public const string DefaultComicEndpointPath = "comic/";

	/// <summary>
	/// Default maximum number of Comick search results requested per query.
	/// </summary>
	public const int DefaultSearchMaxResults = 4;

	/// <summary>
	/// Initializes a new instance of the <see cref="ComickDirectApiClientOptions"/> class with default settings.
	/// </summary>
	public ComickDirectApiClientOptions()
		: this(
			new Uri(DefaultBaseUri, UriKind.Absolute),
			TimeSpan.FromSeconds(DefaultTimeoutSeconds),
			DefaultSearchEndpointPath,
			DefaultSearchMaxResults,
			DefaultComicEndpointPath)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ComickDirectApiClientOptions"/> class.
	/// </summary>
	/// <param name="baseUri">Comick API base URI.</param>
	/// <param name="requestTimeout">HTTP request timeout.</param>
	/// <param name="searchEndpointPath">Relative search endpoint path appended under <paramref name="baseUri"/>.</param>
	/// <param name="comicEndpointPath">Relative comic endpoint path prefix appended under <paramref name="baseUri"/>.</param>
	public ComickDirectApiClientOptions(
		Uri baseUri,
		TimeSpan requestTimeout,
		string searchEndpointPath,
		string comicEndpointPath)
		: this(
			baseUri,
			requestTimeout,
			searchEndpointPath,
			DefaultSearchMaxResults,
			comicEndpointPath)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ComickDirectApiClientOptions"/> class.
	/// </summary>
	/// <param name="baseUri">Comick API base URI.</param>
	/// <param name="requestTimeout">HTTP request timeout.</param>
	/// <param name="searchEndpointPath">Relative search endpoint path appended under <paramref name="baseUri"/>.</param>
	/// <param name="searchMaxResults">Maximum number of search results requested per query.</param>
	/// <param name="comicEndpointPath">Relative comic endpoint path prefix appended under <paramref name="baseUri"/>.</param>
	public ComickDirectApiClientOptions(
		Uri baseUri,
		TimeSpan requestTimeout,
		string searchEndpointPath,
		int searchMaxResults,
		string comicEndpointPath)
	{
		ArgumentNullException.ThrowIfNull(baseUri);
		ArgumentException.ThrowIfNullOrWhiteSpace(searchEndpointPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(comicEndpointPath);

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

		if (searchMaxResults <= 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(searchMaxResults),
				searchMaxResults,
				"Search max results must be > 0.");
		}

		BaseUri = MetadataUriNormalization.EnsureTrailingSlash(baseUri);
		SearchEndpointPath = MetadataUriNormalization.NormalizeEndpointPath(
			searchEndpointPath,
			nameof(searchEndpointPath),
			"Comick search endpoint path");
		SearchMaxResults = searchMaxResults;
		ComicEndpointPath = MetadataUriNormalization.NormalizeEndpointPath(
			comicEndpointPath,
			nameof(comicEndpointPath),
			"Comick comic endpoint path");
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

	/// <summary>
	/// Gets the relative search endpoint path appended under <see cref="BaseUri"/>.
	/// </summary>
	public string SearchEndpointPath
	{
		get;
	}

	/// <summary>
	/// Gets the maximum number of search results requested per query.
	/// </summary>
	public int SearchMaxResults
	{
		get;
	}

	/// <summary>
	/// Gets the relative comic endpoint path prefix appended under <see cref="BaseUri"/>.
	/// </summary>
	public string ComicEndpointPath
	{
		get;
	}

}
