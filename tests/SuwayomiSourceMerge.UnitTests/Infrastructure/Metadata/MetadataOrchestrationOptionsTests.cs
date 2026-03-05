namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="MetadataOrchestrationOptions"/>.
/// </summary>
public sealed class MetadataOrchestrationOptionsTests
{
	/// <summary>
	/// Verifies valid constructor inputs are preserved.
	/// </summary>
	[Fact]
	public void Constructor_Expected_ShouldStoreValues()
	{
		Uri flaresolverrServerUri = new("https://flaresolverr.example.local/");
		MetadataOrchestrationOptions options = new(
			TimeSpan.FromHours(24),
			new Uri("https://api.comick.dev/"),
			"v1.0/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
			"comic/",
			new Uri("https://meo.comick.pictures/"),
			flaresolverrServerUri,
			TimeSpan.FromMinutes(60),
			"en",
			TimeSpan.FromMilliseconds(1000),
			TimeSpan.FromHours(24));

		Assert.Equal(TimeSpan.FromHours(24), options.ComickMetadataCooldown);
		Assert.Equal(new Uri("https://api.comick.dev/"), options.ComickApiBaseUri);
		Assert.Equal("v1.0/search/", options.ComickSearchEndpointPath);
		Assert.Equal(4, options.ComickSearchMaxResults);
		Assert.Equal("comic/", options.ComickComicEndpointPath);
		Assert.Equal(new Uri("https://meo.comick.pictures/"), options.ComickImageBaseUri);
		Assert.Equal(flaresolverrServerUri, options.FlaresolverrServerUri);
		Assert.Equal(TimeSpan.FromMinutes(60), options.FlaresolverrDirectRetryInterval);
		Assert.Equal("en", options.PreferredLanguage);
		Assert.Equal(TimeSpan.FromMilliseconds(1000), options.MetadataApiRequestDelay);
		Assert.Equal(TimeSpan.FromHours(24), options.MetadataApiCacheTtl);
	}

	/// <summary>
	/// Verifies null FlareSolverr URI is accepted for disabled routing mode.
	/// </summary>
	[Fact]
	public void Constructor_Edge_ShouldAllowNullFlaresolverrUri()
	{
		MetadataOrchestrationOptions options = new(
			TimeSpan.FromHours(12),
			new Uri("https://api.comick.dev/"),
			"search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
			"v1.0/comic/",
			new Uri("https://images.example.local"),
			null,
			TimeSpan.FromMinutes(30),
			"ja",
			TimeSpan.Zero,
			TimeSpan.FromHours(24));

		Assert.Null(options.FlaresolverrServerUri);
		Assert.Equal(TimeSpan.Zero, options.MetadataApiRequestDelay);
		Assert.Equal(new Uri("https://images.example.local/"), options.ComickImageBaseUri);
		Assert.Equal("search/", options.ComickSearchEndpointPath);
		Assert.Equal(4, options.ComickSearchMaxResults);
		Assert.Equal("v1.0/comic/", options.ComickComicEndpointPath);
	}

	/// <summary>
	/// Verifies constructor guards reject non-positive intervals and blank language values.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenArgumentsInvalid()
	{
		ArgumentOutOfRangeException cooldownException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.Zero,
				new Uri("https://api.comick.dev/"),
				"v1.0/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
				"comic/",
				new Uri("https://meo.comick.pictures/"),
				null,
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(24)));
		ArgumentOutOfRangeException directRetryIntervalException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				"v1.0/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
				"comic/",
				new Uri("https://meo.comick.pictures/"),
				null,
				TimeSpan.Zero,
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(24)));
		ArgumentException preferredLanguageException = Assert.ThrowsAny<ArgumentException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				"v1.0/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
				"comic/",
				new Uri("https://meo.comick.pictures/"),
				null,
				TimeSpan.FromMinutes(60),
					" ",
					TimeSpan.FromMilliseconds(1000),
					TimeSpan.FromHours(24)));
		ArgumentOutOfRangeException searchMaxResultsException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				"v1.0/search/",
				0,
				"comic/",
				new Uri("https://meo.comick.pictures/"),
				null,
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(24)));

		Assert.Equal("comickMetadataCooldown", cooldownException.ParamName);
		Assert.Equal("flaresolverrDirectRetryInterval", directRetryIntervalException.ParamName);
		Assert.Equal("preferredLanguage", preferredLanguageException.ParamName);
		Assert.Equal("comickSearchMaxResults", searchMaxResultsException.ParamName);
	}

	/// <summary>
	/// Verifies constructor guards reject invalid metadata API pacing and cache TTL values.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenMetadataApiTimingValuesInvalid()
	{
		ArgumentOutOfRangeException metadataApiRequestDelayException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				"v1.0/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
				"comic/",
				new Uri("https://meo.comick.pictures/"),
				null,
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(-1),
				TimeSpan.FromHours(24)));
		ArgumentOutOfRangeException metadataApiCacheTtlZeroException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				"v1.0/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
				"comic/",
				new Uri("https://meo.comick.pictures/"),
				null,
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.Zero));
		ArgumentOutOfRangeException metadataApiCacheTtlNegativeException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				"v1.0/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
				"comic/",
				new Uri("https://meo.comick.pictures/"),
				null,
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(-1)));

		Assert.Equal("metadataApiRequestDelay", metadataApiRequestDelayException.ParamName);
		Assert.Equal("metadataApiCacheTtl", metadataApiCacheTtlZeroException.ParamName);
		Assert.Equal("metadataApiCacheTtl", metadataApiCacheTtlNegativeException.ParamName);
	}

	/// <summary>
	/// Verifies constructor guards reject relative and unsupported FlareSolverr URI values.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenFlaresolverrUriInvalid()
	{
		ArgumentException relativeException = Assert.Throws<ArgumentException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				"v1.0/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
				"comic/",
				new Uri("https://meo.comick.pictures/"),
				new Uri("/v1", UriKind.Relative),
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(24)));
		ArgumentException schemeException = Assert.Throws<ArgumentException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				"v1.0/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
				"comic/",
				new Uri("https://meo.comick.pictures/"),
				new Uri("ftp://flaresolverr.example.local/"),
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(24)));

		Assert.Equal("flaresolverrServerUri", relativeException.ParamName);
		Assert.Equal("flaresolverrServerUri", schemeException.ParamName);
	}

	/// <summary>
	/// Verifies constructor guards reject invalid Comick base/image URIs and endpoint paths.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenComickEndpointArgumentsInvalid()
	{
		ArgumentException invalidComickBaseUriException = Assert.Throws<ArgumentException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("ftp://api.comick.dev/"),
				"v1.0/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
				"comic/",
				new Uri("https://meo.comick.pictures/"),
				null,
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(24)));
		ArgumentException invalidSearchPathException = Assert.Throws<ArgumentException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				" / ",
				"comic/",
				new Uri("https://meo.comick.pictures/"),
				null,
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(24)));
		ArgumentException invalidComicPathException = Assert.Throws<ArgumentException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				"v1.0/search/",
				" / ",
				new Uri("https://meo.comick.pictures/"),
				null,
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(24)));
		ArgumentException invalidComickImageUriException = Assert.Throws<ArgumentException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				"v1.0/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
				"comic/",
				new Uri("ftp://meo.comick.pictures/"),
				null,
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(24)));
		ArgumentException absoluteSearchPathException = Assert.Throws<ArgumentException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				"https://override.example/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
				"comic/",
				new Uri("https://meo.comick.pictures/"),
				null,
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(24)));
		ArgumentException queryComicPathException = Assert.Throws<ArgumentException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("https://api.comick.dev/"),
				"v1.0/search/",
				ComickDirectApiClientOptions.DefaultSearchMaxResults,
				"comic/?v=1",
				new Uri("https://meo.comick.pictures/"),
				null,
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(24)));

		Assert.Equal("comickApiBaseUri", invalidComickBaseUriException.ParamName);
		Assert.Equal("comickSearchEndpointPath", invalidSearchPathException.ParamName);
		Assert.Equal("comickComicEndpointPath", invalidComicPathException.ParamName);
		Assert.Equal("comickImageBaseUri", invalidComickImageUriException.ParamName);
		Assert.Equal("comickSearchEndpointPath", absoluteSearchPathException.ParamName);
		Assert.Equal("comickComicEndpointPath", queryComicPathException.ParamName);
	}
}
