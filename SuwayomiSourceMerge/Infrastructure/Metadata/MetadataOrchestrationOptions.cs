using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Carries normalized runtime settings that control Comick metadata orchestration behavior.
/// </summary>
internal sealed class MetadataOrchestrationOptions
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataOrchestrationOptions"/> class.
	/// </summary>
	/// <param name="comickMetadataCooldown">Per-title cooldown window for Comick metadata requests.</param>
	/// <param name="comickApiBaseUri">Comick API base URI used for metadata requests.</param>
	/// <param name="comickSearchEndpointPath">Relative Comick search endpoint path appended under <paramref name="comickApiBaseUri"/>.</param>
	/// <param name="comickComicEndpointPath">Relative Comick comic-detail endpoint path appended under <paramref name="comickApiBaseUri"/>.</param>
	/// <param name="comickImageBaseUri">Comick image base URI used to resolve relative cover keys.</param>
	/// <param name="flaresolverrServerUri">Optional FlareSolverr server URI. <see langword="null"/> disables FlareSolverr routing.</param>
	/// <param name="flaresolverrDirectRetryInterval">Retry interval before probing direct Comick access after sticky FlareSolverr routing.</param>
	/// <param name="preferredLanguage">Preferred language code used for metadata canonical-title selection.</param>
	/// <param name="metadataApiRequestDelay">Delay applied between outbound metadata API requests.</param>
	/// <param name="metadataApiCacheTtl">TTL for persisted metadata API cache entries.</param>
	public MetadataOrchestrationOptions(
		TimeSpan comickMetadataCooldown,
		Uri comickApiBaseUri,
		string comickSearchEndpointPath,
		string comickComicEndpointPath,
		Uri comickImageBaseUri,
		Uri? flaresolverrServerUri,
		TimeSpan flaresolverrDirectRetryInterval,
		string preferredLanguage,
		TimeSpan metadataApiRequestDelay,
		TimeSpan metadataApiCacheTtl)
		: this(
			comickMetadataCooldown,
			comickApiBaseUri,
			comickSearchEndpointPath,
			ComickDirectApiClientOptions.DefaultSearchMaxResults,
			comickComicEndpointPath,
			comickImageBaseUri,
			flaresolverrServerUri,
			flaresolverrDirectRetryInterval,
			preferredLanguage,
			metadataApiRequestDelay,
			metadataApiCacheTtl)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataOrchestrationOptions"/> class.
	/// </summary>
	/// <param name="comickMetadataCooldown">Per-title cooldown window for Comick metadata requests.</param>
	/// <param name="comickApiBaseUri">Comick API base URI used for metadata requests.</param>
	/// <param name="comickSearchEndpointPath">Relative Comick search endpoint path appended under <paramref name="comickApiBaseUri"/>.</param>
	/// <param name="comickSearchMaxResults">Maximum number of Comick search results requested per query.</param>
	/// <param name="comickComicEndpointPath">Relative Comick comic-detail endpoint path appended under <paramref name="comickApiBaseUri"/>.</param>
	/// <param name="comickImageBaseUri">Comick image base URI used to resolve relative cover keys.</param>
	/// <param name="flaresolverrServerUri">Optional FlareSolverr server URI. <see langword="null"/> disables FlareSolverr routing.</param>
	/// <param name="flaresolverrDirectRetryInterval">Retry interval before probing direct Comick access after sticky FlareSolverr routing.</param>
	/// <param name="preferredLanguage">Preferred language code used for metadata canonical-title selection.</param>
	/// <param name="metadataApiRequestDelay">Delay applied between outbound metadata API requests.</param>
	/// <param name="metadataApiCacheTtl">TTL for persisted metadata API cache entries.</param>
	public MetadataOrchestrationOptions(
		TimeSpan comickMetadataCooldown,
		Uri comickApiBaseUri,
		string comickSearchEndpointPath,
		int comickSearchMaxResults,
		string comickComicEndpointPath,
		Uri comickImageBaseUri,
		Uri? flaresolverrServerUri,
		TimeSpan flaresolverrDirectRetryInterval,
		string preferredLanguage,
		TimeSpan metadataApiRequestDelay,
		TimeSpan metadataApiCacheTtl)
	{
		ArgumentNullException.ThrowIfNull(comickApiBaseUri);
		ArgumentException.ThrowIfNullOrWhiteSpace(comickSearchEndpointPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(comickComicEndpointPath);
		ArgumentNullException.ThrowIfNull(comickImageBaseUri);
		ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguage);

		if (comickMetadataCooldown <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(comickMetadataCooldown),
				comickMetadataCooldown,
				"Comick metadata cooldown must be > 0.");
		}

		if (flaresolverrDirectRetryInterval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(flaresolverrDirectRetryInterval),
				flaresolverrDirectRetryInterval,
				"FlareSolverr direct retry interval must be > 0.");
		}

		if (comickSearchMaxResults <= 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(comickSearchMaxResults),
				comickSearchMaxResults,
				"Comick search max results must be > 0.");
		}

		if (metadataApiRequestDelay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(metadataApiRequestDelay),
				metadataApiRequestDelay,
				"Metadata API request delay must be >= 0.");
		}

		if (metadataApiCacheTtl <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(metadataApiCacheTtl),
				metadataApiCacheTtl,
				"Metadata API cache TTL must be > 0.");
		}

		EnsureAbsoluteHttpOrHttpsUri(comickApiBaseUri, nameof(comickApiBaseUri), "Comick API base URI");
		EnsureAbsoluteHttpOrHttpsUri(comickImageBaseUri, nameof(comickImageBaseUri), "Comick image base URI");

		if (flaresolverrServerUri is not null)
		{
			if (!flaresolverrServerUri.IsAbsoluteUri)
			{
				throw new ArgumentException(
					"FlareSolverr server URI must be absolute when provided.",
					nameof(flaresolverrServerUri));
			}

			if (!string.Equals(flaresolverrServerUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(flaresolverrServerUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
			{
				throw new ArgumentException(
					"FlareSolverr server URI must use http or https when provided.",
					nameof(flaresolverrServerUri));
			}
		}

		ComickMetadataCooldown = comickMetadataCooldown;
		ComickApiBaseUri = MetadataUriNormalization.EnsureTrailingSlash(comickApiBaseUri);
		ComickSearchEndpointPath = NormalizeEndpointPath(comickSearchEndpointPath, nameof(comickSearchEndpointPath), "Comick search endpoint path");
		ComickSearchMaxResults = comickSearchMaxResults;
		ComickComicEndpointPath = NormalizeEndpointPath(comickComicEndpointPath, nameof(comickComicEndpointPath), "Comick comic endpoint path");
		ComickImageBaseUri = MetadataUriNormalization.EnsureTrailingSlash(comickImageBaseUri);
		FlaresolverrServerUri = flaresolverrServerUri;
		FlaresolverrDirectRetryInterval = flaresolverrDirectRetryInterval;
		PreferredLanguage = preferredLanguage.Trim();
		MetadataApiRequestDelay = metadataApiRequestDelay;
		MetadataApiCacheTtl = metadataApiCacheTtl;
	}

	/// <summary>
	/// Gets the per-title cooldown window for Comick metadata requests.
	/// </summary>
	public TimeSpan ComickMetadataCooldown
	{
		get;
	}

	/// <summary>
	/// Gets the Comick API base URI used for metadata requests.
	/// </summary>
	public Uri ComickApiBaseUri
	{
		get;
	}

	/// <summary>
	/// Gets the relative Comick search endpoint path appended under <see cref="ComickApiBaseUri"/>.
	/// </summary>
	public string ComickSearchEndpointPath
	{
		get;
	}

	/// <summary>
	/// Gets the maximum number of Comick search results requested per query.
	/// </summary>
	public int ComickSearchMaxResults
	{
		get;
	}

	/// <summary>
	/// Gets the relative Comick comic-detail endpoint path appended under <see cref="ComickApiBaseUri"/>.
	/// </summary>
	public string ComickComicEndpointPath
	{
		get;
	}

	/// <summary>
	/// Gets the Comick image base URI used to resolve relative cover keys.
	/// </summary>
	public Uri ComickImageBaseUri
	{
		get;
	}

	/// <summary>
	/// Gets the optional FlareSolverr server URI.
	/// </summary>
	public Uri? FlaresolverrServerUri
	{
		get;
	}

	/// <summary>
	/// Gets the interval before probing direct Comick access again after sticky FlareSolverr routing.
	/// </summary>
	public TimeSpan FlaresolverrDirectRetryInterval
	{
		get;
	}

	/// <summary>
	/// Gets the preferred language code used for metadata canonical-title selection.
	/// </summary>
	public string PreferredLanguage
	{
		get;
	}

	/// <summary>
	/// Gets the pacing delay applied between outbound metadata API requests.
	/// </summary>
	public TimeSpan MetadataApiRequestDelay
	{
		get;
	}

	/// <summary>
	/// Gets the TTL for persisted metadata API cache entries.
	/// </summary>
	public TimeSpan MetadataApiCacheTtl
	{
		get;
	}

	/// <summary>
	/// Ensures one URI is absolute and uses HTTP or HTTPS.
	/// </summary>
	/// <param name="value">URI value.</param>
	/// <param name="paramName">Guard parameter name.</param>
	/// <param name="description">Guard message descriptor.</param>
	private static void EnsureAbsoluteHttpOrHttpsUri(Uri value, string paramName, string description)
	{
		ArgumentNullException.ThrowIfNull(value);

		if (!value.IsAbsoluteUri)
		{
			throw new ArgumentException($"{description} must be absolute.", paramName);
		}

		if (!string.Equals(value.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(value.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException($"{description} must use http or https.", paramName);
		}
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
