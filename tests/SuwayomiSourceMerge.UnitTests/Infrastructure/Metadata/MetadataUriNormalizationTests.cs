namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="MetadataUriNormalization"/>.
/// </summary>
public sealed class MetadataUriNormalizationTests
{
	/// <summary>
	/// Verifies absolute URIs are normalized to exactly one trailing slash.
	/// </summary>
	[Fact]
	public void EnsureTrailingSlash_Expected_ShouldNormalizeAbsoluteUri()
	{
		Uri result = MetadataUriNormalization.EnsureTrailingSlash(new Uri("https://api.comick.dev/v1.0"));

		Assert.Equal("https://api.comick.dev/v1.0/", result.AbsoluteUri);
	}

	/// <summary>
	/// Verifies trailing slash normalization preserves already-normalized absolute URI values.
	/// </summary>
	[Fact]
	public void EnsureTrailingSlash_Edge_ShouldPreserveSingleTrailingSlash_WhenAlreadyNormalized()
	{
		Uri result = MetadataUriNormalization.EnsureTrailingSlash(new Uri("https://api.comick.dev/v1.0/"));

		Assert.Equal("https://api.comick.dev/v1.0/", result.AbsoluteUri);
	}

	/// <summary>
	/// Verifies null and relative URI values are rejected.
	/// </summary>
	[Fact]
	public void EnsureTrailingSlash_Failure_ShouldThrow_WhenUriInvalid()
	{
		ArgumentNullException nullException = Assert.Throws<ArgumentNullException>(
			() => MetadataUriNormalization.EnsureTrailingSlash(null!));
		ArgumentException relativeException = Assert.Throws<ArgumentException>(
			() => MetadataUriNormalization.EnsureTrailingSlash(new Uri("/relative", UriKind.Relative)));

		Assert.Equal("value", nullException.ParamName);
		Assert.Equal("value", relativeException.ParamName);
	}
}
