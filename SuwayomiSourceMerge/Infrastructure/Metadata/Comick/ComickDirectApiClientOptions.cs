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

		BaseUri = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
		SearchEndpointPath = NormalizeEndpointPath(
			searchEndpointPath,
			nameof(searchEndpointPath),
			"Comick search endpoint path");
		SearchMaxResults = searchMaxResults;
		ComicEndpointPath = NormalizeEndpointPath(
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

	/// <summary>
	/// Normalizes one endpoint path to a non-root relative path with exactly one trailing slash.
	/// </summary>
	/// <param name="value">Endpoint path value.</param>
	/// <param name="paramName">Guard parameter name.</param>
	/// <param name="description">Guard message descriptor.</param>
	/// <returns>Normalized endpoint path.</returns>
	private static string NormalizeEndpointPath(string value, string paramName, string description)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);

		string trimmed = value.Trim();
		if (HasUriSchemePrefix(trimmed))
		{
			throw new ArgumentException($"{description} must be relative and must not include a URI scheme/host.", paramName);
		}

		string normalized = trimmed.TrimStart('/');
		if (normalized.Length == 0)
		{
			throw new ArgumentException($"{description} must not resolve to root.", paramName);
		}

		if (normalized.IndexOf('?') >= 0 || normalized.IndexOf('#') >= 0)
		{
			throw new ArgumentException($"{description} must not include query or fragment components.", paramName);
		}

		return normalized.TrimEnd('/') + "/";
	}

	/// <summary>
	/// Determines whether one value starts with a URI scheme token.
	/// </summary>
	/// <param name="value">Raw value.</param>
	/// <returns><see langword="true"/> when a URI scheme prefix is present; otherwise <see langword="false"/>.</returns>
	private static bool HasUriSchemePrefix(string value)
	{
		int schemeSeparatorIndex = value.IndexOf(':');
		if (schemeSeparatorIndex <= 0)
		{
			return false;
		}

		ReadOnlySpan<char> schemeToken = value.AsSpan(0, schemeSeparatorIndex);
		if (!char.IsLetter(schemeToken[0]))
		{
			return false;
		}

		for (int index = 1; index < schemeToken.Length; index++)
		{
			char character = schemeToken[index];
			if (!char.IsLetterOrDigit(character) && character != '+' && character != '-' && character != '.')
			{
				return false;
			}
		}

		return true;
	}
}
