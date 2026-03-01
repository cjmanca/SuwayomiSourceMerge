using System.IO;
using System.Net;
using System.Threading.Tasks;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

public sealed partial class OverrideCoverServiceTests
{
	[Fact]
	public async Task EnsureCoverJpgAsync_Expected_ShouldNotTriggerThrottle_WhenPreferredAlreadyExists()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredDirectory = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		
		// Create the cover so that it already exists
		string coverPath = Path.Combine(preferredDirectory, "cover.jpg");
		await File.WriteAllBytesAsync(coverPath, _onePixelJpegPayload);

		RecordingHttpMessageHandler handler = new(_ => CreateResponse(HttpStatusCode.OK, _onePixelJpegPayload, "image/jpeg"));
		using HttpClient httpClient = new(handler);
		TrackingMetadataApiRequestThrottle throttle = new();

		using OverrideCoverService service = new(httpClient, coverBaseUri: null, throttle: throttle);
		OverrideCoverRequest request = new(preferredDirectory, [preferredDirectory], "covers/sample.jpg");

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(request);

		Assert.Equal(OverrideCoverOutcome.AlreadyExists, result.Outcome);
		Assert.Equal(0, throttle.CallCount); // Important: The throttle should not have been invoked
		Assert.Equal(0, handler.CallCount); // The network should not have been hit either
	}

	[Fact]
	public async Task EnsureCoverJpgAsync_Expected_ShouldTriggerThrottle_WhenDownloadingNewCover()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredDirectory = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");

		RecordingHttpMessageHandler handler = new(_ => CreateResponse(HttpStatusCode.OK, _onePixelJpegPayload, "image/jpeg"));
		using HttpClient httpClient = new(handler);
		TrackingMetadataApiRequestThrottle throttle = new();

		using OverrideCoverService service = new(httpClient, coverBaseUri: null, throttle: throttle);
		OverrideCoverRequest request = new(preferredDirectory, [preferredDirectory], "covers/sample.jpg");

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(request);

		Assert.Equal(OverrideCoverOutcome.WrittenDownloadedJpeg, result.Outcome);
		Assert.Equal(1, throttle.CallCount); // Throttle should correctly wrap the download
		Assert.Equal(1, handler.CallCount); // Network was hit once
	}
}
