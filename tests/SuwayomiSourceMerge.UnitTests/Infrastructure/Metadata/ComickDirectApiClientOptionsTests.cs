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
			TimeSpan.FromSeconds(10));

		Assert.Equal(new Uri("https://api.comick.dev/"), options.BaseUri);
	}

	/// <summary>
	/// Verifies invalid arguments throw deterministic guard exceptions.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenArgumentsInvalid()
	{
		Assert.Throws<ArgumentNullException>(
			() => new ComickDirectApiClientOptions(
				null!,
				TimeSpan.FromSeconds(10)));

		Assert.Throws<ArgumentException>(
			() => new ComickDirectApiClientOptions(
				new Uri("/relative", UriKind.Relative),
				TimeSpan.FromSeconds(10)));

		Assert.Throws<ArgumentException>(
			() => new ComickDirectApiClientOptions(
				new Uri("ftp://api.comick.dev/"),
				TimeSpan.FromSeconds(10)));

		Assert.Throws<ArgumentOutOfRangeException>(
			() => new ComickDirectApiClientOptions(
				new Uri("https://api.comick.dev/"),
				TimeSpan.Zero));
	}
}
