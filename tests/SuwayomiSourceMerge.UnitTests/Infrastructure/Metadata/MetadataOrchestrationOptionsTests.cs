namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata;

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
			flaresolverrServerUri,
			TimeSpan.FromMinutes(60),
			"en");

		Assert.Equal(TimeSpan.FromHours(24), options.ComickMetadataCooldown);
		Assert.Equal(flaresolverrServerUri, options.FlaresolverrServerUri);
		Assert.Equal(TimeSpan.FromMinutes(60), options.FlaresolverrDirectRetryInterval);
		Assert.Equal("en", options.PreferredLanguage);
	}

	/// <summary>
	/// Verifies null FlareSolverr URI is accepted for disabled routing mode.
	/// </summary>
	[Fact]
	public void Constructor_Edge_ShouldAllowNullFlaresolverrUri()
	{
		MetadataOrchestrationOptions options = new(
			TimeSpan.FromHours(12),
			null,
			TimeSpan.FromMinutes(30),
			"ja");

		Assert.Null(options.FlaresolverrServerUri);
	}

	/// <summary>
	/// Verifies constructor guards reject non-positive intervals and blank language values.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenArgumentsInvalid()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.Zero,
				null,
				TimeSpan.FromMinutes(60),
				"en"));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				null,
				TimeSpan.Zero,
				"en"));
		Assert.ThrowsAny<ArgumentException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				null,
				TimeSpan.FromMinutes(60),
				" "));
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
				new Uri("/v1", UriKind.Relative),
				TimeSpan.FromMinutes(60),
				"en"));
		ArgumentException schemeException = Assert.Throws<ArgumentException>(
			() => new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				new Uri("ftp://flaresolverr.example.local/"),
				TimeSpan.FromMinutes(60),
				"en"));

		Assert.Equal("flaresolverrServerUri", relativeException.ParamName);
		Assert.Equal("flaresolverrServerUri", schemeException.ParamName);
	}
}
