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
	/// <param name="flaresolverrServerUri">Optional FlareSolverr server URI. <see langword="null"/> disables FlareSolverr routing.</param>
	/// <param name="flaresolverrDirectRetryInterval">Retry interval before probing direct Comick access after sticky FlareSolverr routing.</param>
	/// <param name="preferredLanguage">Preferred language code used for metadata canonical-title selection.</param>
	public MetadataOrchestrationOptions(
		TimeSpan comickMetadataCooldown,
		Uri? flaresolverrServerUri,
		TimeSpan flaresolverrDirectRetryInterval,
		string preferredLanguage)
	{
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
		FlaresolverrServerUri = flaresolverrServerUri;
		FlaresolverrDirectRetryInterval = flaresolverrDirectRetryInterval;
		PreferredLanguage = preferredLanguage.Trim();
	}

	/// <summary>
	/// Gets the per-title cooldown window for Comick metadata requests.
	/// </summary>
	public TimeSpan ComickMetadataCooldown
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
}
