namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="FlaresolverrClientOptions"/>.
/// </summary>
public sealed class FlaresolverrClientOptionsTests
{
	/// <summary>
	/// Verifies constructor accepts absolute http/https URIs with positive timeout values.
	/// </summary>
	[Fact]
	public void Constructor_Expected_ShouldAcceptAbsoluteUriAndPositiveTimeout()
	{
		FlaresolverrClientOptions options = new(
			new Uri("https://flaresolverr.example.local/"),
			TimeSpan.FromSeconds(25));

		Assert.Equal("https://flaresolverr.example.local/", options.BaseUri.AbsoluteUri);
		Assert.Equal(TimeSpan.FromSeconds(25), options.RequestTimeout);
	}

	/// <summary>
	/// Verifies base URI normalization appends one trailing slash when missing.
	/// </summary>
	[Fact]
	public void Constructor_Edge_ShouldNormalizeBaseUriWithTrailingSlash()
	{
		FlaresolverrClientOptions options = new(new Uri("https://flaresolverr.example.local"));

		Assert.Equal("https://flaresolverr.example.local/", options.BaseUri.AbsoluteUri);
		Assert.Equal(TimeSpan.FromSeconds(FlaresolverrClientOptions.DefaultTimeoutSeconds), options.RequestTimeout);
	}

	/// <summary>
	/// Verifies constructor rejects relative URI, unsupported scheme, and non-positive timeout values.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenUriOrTimeoutInvalid()
	{
		ArgumentException relativeException = Assert.Throws<ArgumentException>(
			() => new FlaresolverrClientOptions(
				new Uri("/v1", UriKind.Relative),
				TimeSpan.FromSeconds(1)));
		ArgumentException schemeException = Assert.Throws<ArgumentException>(
			() => new FlaresolverrClientOptions(
				new Uri("ftp://flaresolverr.example.local/"),
				TimeSpan.FromSeconds(1)));
		ArgumentOutOfRangeException timeoutException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new FlaresolverrClientOptions(
				new Uri("https://flaresolverr.example.local/"),
				TimeSpan.Zero));

		Assert.Equal("baseUri", relativeException.ParamName);
		Assert.Equal("baseUri", schemeException.ParamName);
		Assert.Equal("requestTimeout", timeoutException.ParamName);
	}
}
