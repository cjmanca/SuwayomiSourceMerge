namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="ComickDirectApiClientOptions"/>.
/// </summary>
public sealed class ComickDirectApiClientOptionsTests
{
	/// <summary>
	/// Verifies default constructor returns documented base URI and timeout values.
	/// </summary>
	[Fact]
	public void Constructor_Expected_ShouldUseDocumentedDefaults()
	{
		ComickDirectApiClientOptions options = new();

		Assert.Equal(new Uri("https://api.comick.dev/"), options.BaseUri);
		Assert.Equal(ComickDirectApiClientOptions.DefaultSearchEndpointPath, options.SearchEndpointPath);
		Assert.Equal(ComickDirectApiClientOptions.DefaultSearchMaxResults, options.SearchMaxResults);
		Assert.Equal(ComickDirectApiClientOptions.DefaultComicEndpointPath, options.ComicEndpointPath);
		Assert.Equal(TimeSpan.FromSeconds(ComickDirectApiClientOptions.DefaultTimeoutSeconds), options.RequestTimeout);
	}

	/// <summary>
	/// Verifies base URI normalization adds one trailing slash.
	/// </summary>
	[Fact]
	public void Constructor_Edge_ShouldNormalizeBaseUriTrailingSlash()
	{
		ComickDirectApiClientOptions options = new(
			new Uri("https://api.comick.dev"),
			TimeSpan.FromSeconds(10),
			searchEndpointPath: "/search",
			searchMaxResults: 25,
			comicEndpointPath: "v1.0/comic");

		Assert.Equal(new Uri("https://api.comick.dev/"), options.BaseUri);
		Assert.Equal("search/", options.SearchEndpointPath);
		Assert.Equal(25, options.SearchMaxResults);
		Assert.Equal("v1.0/comic/", options.ComicEndpointPath);
	}

	/// <summary>
	/// Verifies invalid arguments throw deterministic guard exceptions.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenArgumentsInvalid()
	{
		ArgumentNullException nullBaseUriException = Assert.Throws<ArgumentNullException>(
			() => new ComickDirectApiClientOptions(
				null!,
				TimeSpan.FromSeconds(10),
				ComickDirectApiClientOptions.DefaultSearchEndpointPath,
				ComickDirectApiClientOptions.DefaultComicEndpointPath));

		ArgumentException relativeBaseUriException = Assert.Throws<ArgumentException>(
			() => new ComickDirectApiClientOptions(
				new Uri("/relative", UriKind.Relative),
				TimeSpan.FromSeconds(10),
				ComickDirectApiClientOptions.DefaultSearchEndpointPath,
				ComickDirectApiClientOptions.DefaultComicEndpointPath));

		ArgumentException unsupportedSchemeException = Assert.Throws<ArgumentException>(
			() => new ComickDirectApiClientOptions(
				new Uri("ftp://api.comick.dev/"),
				TimeSpan.FromSeconds(10),
				ComickDirectApiClientOptions.DefaultSearchEndpointPath,
				ComickDirectApiClientOptions.DefaultComicEndpointPath));

		ArgumentOutOfRangeException requestTimeoutException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new ComickDirectApiClientOptions(
				new Uri("https://api.comick.dev/"),
				TimeSpan.Zero,
				ComickDirectApiClientOptions.DefaultSearchEndpointPath,
				ComickDirectApiClientOptions.DefaultComicEndpointPath));
		ArgumentException absoluteSearchPathException = Assert.Throws<ArgumentException>(
			() => new ComickDirectApiClientOptions(
				new Uri("https://api.comick.dev/"),
				TimeSpan.FromSeconds(10),
				"https://malicious.example/search/",
				ComickDirectApiClientOptions.DefaultComicEndpointPath));
		ArgumentException querySearchPathException = Assert.Throws<ArgumentException>(
			() => new ComickDirectApiClientOptions(
				new Uri("https://api.comick.dev/"),
				TimeSpan.FromSeconds(10),
				"v1.0/search/?q=already",
				ComickDirectApiClientOptions.DefaultComicEndpointPath));
			ArgumentException fragmentComicPathException = Assert.Throws<ArgumentException>(
				() => new ComickDirectApiClientOptions(
					new Uri("https://api.comick.dev/"),
					TimeSpan.FromSeconds(10),
					ComickDirectApiClientOptions.DefaultSearchEndpointPath,
					"comic/#anchor"));
			ArgumentOutOfRangeException searchMaxResultsException = Assert.Throws<ArgumentOutOfRangeException>(
				() => new ComickDirectApiClientOptions(
					new Uri("https://api.comick.dev/"),
					TimeSpan.FromSeconds(10),
					ComickDirectApiClientOptions.DefaultSearchEndpointPath,
					0,
					ComickDirectApiClientOptions.DefaultComicEndpointPath));

		Assert.Equal("baseUri", nullBaseUriException.ParamName);
		Assert.Equal("baseUri", relativeBaseUriException.ParamName);
		Assert.Equal("baseUri", unsupportedSchemeException.ParamName);
		Assert.Equal("requestTimeout", requestTimeoutException.ParamName);
		Assert.Equal("searchEndpointPath", absoluteSearchPathException.ParamName);
		Assert.Equal("searchEndpointPath", querySearchPathException.ParamName);
		Assert.Equal("comicEndpointPath", fragmentComicPathException.ParamName);
		Assert.Equal("searchMaxResults", searchMaxResultsException.ParamName);
	}
}
