namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using System.Text;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="OverrideCoverService"/>.
/// </summary>
public sealed partial class OverrideCoverServiceTests
{
	/// <summary>
	/// One-pixel GIF payload fixture.
	/// </summary>
	private static readonly byte[] _onePixelGifPayload = Convert.FromBase64String(
		"R0lGODlhAQABAIABAP///wAAACwAAAAAAQABAAACAkQBADs=");

	/// <summary>
	/// One-pixel JPEG payload fixture.
	/// </summary>
	private static readonly byte[] _onePixelJpegPayload = Convert.FromBase64String(
		"/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0a" +
		"HBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIy" +
		"MjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDAREA" +
		"AhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAb/xAAVEAEBAAAAAAAAAAAAAAAAAAAABP/aAAwD" +
		"AQACEAMQAAABqA//xAAVEQEBAAAAAAAAAAAAAAAAAAAAEf/aAAgBAQABPwCX/8QAFBEBAAAAAAAAA" +
		"AAAAAAAAAAAEP/aAAgBAgEBPwCf/8QAFBEBAAAAAAAAAAAAAAAAAAAAEP/aAAgBAwEBPwCf/9k=");

	/// <summary>
	/// Verifies relative cover keys resolve against Comick cover base URI and write downloaded JPEG payloads.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Expected_ShouldWriteDownloadedJpeg_WhenRelativeCoverKeyProvided()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string secondaryOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "disk1", "Manga Title");
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, _onePixelJpegPayload, "image/jpeg"));
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		OverrideCoverRequest request = new(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath, secondaryOverrideDirectoryPath],
			"covers/sample.jpg");

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(request);
		string expectedCoverPath = Path.Combine(preferredOverrideDirectoryPath, "cover.jpg");

		Assert.Equal(OverrideCoverOutcome.WrittenDownloadedJpeg, result.Outcome);
		Assert.Equal(expectedCoverPath, result.CoverJpgPath);
		Assert.True(result.CoverJpgExists);
		Assert.Null(result.ExistingCoverPath);
		Assert.NotNull(result.CoverUri);
		Assert.Equal("https://meo.comick.pictures/covers/sample.jpg", result.CoverUri!.AbsoluteUri);
		Assert.Equal(_onePixelJpegPayload, File.ReadAllBytes(expectedCoverPath));
		Assert.NotNull(handler.LastRequest);
		Assert.Equal("https://meo.comick.pictures/covers/sample.jpg", handler.LastRequest!.RequestUri!.AbsoluteUri);
	}
	/// <summary>
	/// Verifies non-JPEG payloads are converted to JPEG before writing.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Expected_ShouldConvertToJpeg_WhenPayloadIsNonJpeg()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, _onePixelGifPayload, "image/gif"));
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		OverrideCoverRequest request = new(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			"covers/sample.png");

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(request);
		string coverPath = Path.Combine(preferredOverrideDirectoryPath, "cover.jpg");
		Assert.True(File.Exists(coverPath));
		byte[] writtenBytes = File.ReadAllBytes(coverPath);

		Assert.Equal(OverrideCoverOutcome.WrittenConvertedJpeg, result.Outcome);
		Assert.True(result.CoverJpgExists);
		Assert.True(writtenBytes.Length > 3);
		Assert.Equal(0xFF, writtenBytes[0]);
		Assert.Equal(0xD8, writtenBytes[1]);
		Assert.Equal(0xFF, writtenBytes[2]);
	}

	/// <summary>
	/// Verifies absolute cover URIs are used as-is.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Edge_ShouldUseAbsoluteCoverUri_AsIs()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, _onePixelJpegPayload, "image/jpeg"));
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		OverrideCoverRequest request = new(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			"https://cdn.example.test/covers/cover.webp");

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(request);

		Assert.Equal(OverrideCoverOutcome.WrittenDownloadedJpeg, result.Outcome);
		Assert.NotNull(handler.LastRequest);
		Assert.Equal("https://cdn.example.test/covers/cover.webp", handler.LastRequest!.RequestUri!.AbsoluteUri);
	}

	/// <summary>
	/// Verifies existing covers in non-preferred override directories short-circuit without network calls.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Edge_ShouldReturnAlreadyExists_WhenAnyOverrideAlreadyContainsCover()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string secondaryOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "disk1", "Manga Title");
		string existingCoverPath = Path.Combine(secondaryOverrideDirectoryPath, "cover.jpg");
		File.WriteAllBytes(existingCoverPath, _onePixelJpegPayload);

		ThrowingHttpMessageHandler handler = new();
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		OverrideCoverRequest request = new(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath, secondaryOverrideDirectoryPath],
			"covers/sample.jpg");

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(request);

		Assert.Equal(OverrideCoverOutcome.AlreadyExists, result.Outcome);
		Assert.Equal(existingCoverPath, result.CoverJpgPath);
		Assert.True(result.CoverJpgExists);
		Assert.Equal(existingCoverPath, result.ExistingCoverPath);
		Assert.Equal(0, handler.CallCount);
		Assert.False(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "cover.jpg")));
	}

	/// <summary>
	/// Verifies existing covers in the preferred override directory short-circuit with preferred-path result semantics.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Edge_ShouldReturnAlreadyExists_WithPreferredPath_WhenPreferredCoverExists()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string preferredCoverPath = Path.Combine(preferredOverrideDirectoryPath, "cover.jpg");
		File.WriteAllBytes(preferredCoverPath, _onePixelJpegPayload);

		ThrowingHttpMessageHandler handler = new();
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(
			new OverrideCoverRequest(
				preferredOverrideDirectoryPath,
				[preferredOverrideDirectoryPath],
				"covers/sample.jpg"));

		Assert.Equal(OverrideCoverOutcome.AlreadyExists, result.Outcome);
		Assert.Equal(preferredCoverPath, result.CoverJpgPath);
		Assert.True(result.CoverJpgExists);
		Assert.Equal(preferredCoverPath, result.ExistingCoverPath);
		Assert.Equal(0, handler.CallCount);
	}

	/// <summary>
	/// Verifies concurrent writes are race-safe and non-destructive.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Edge_ShouldReturnAlreadyExists_ForOneCaller_WhenConcurrentWritesRace()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		ConcurrentGateHttpMessageHandler handler = new(_onePixelJpegPayload, expectedCallCount: 2);
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);
		OverrideCoverRequest request = new(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			"covers/sample.jpg");

		Task<OverrideCoverResult> firstTask = service.EnsureCoverJpgAsync(request);
		Task<OverrideCoverResult> secondTask = service.EnsureCoverJpgAsync(request);

		await handler.WaitUntilExpectedCallsAsync();
		handler.ReleaseResponses();

		OverrideCoverResult[] results = await Task.WhenAll(firstTask, secondTask);
		int writtenCount = results.Count(
			static result => result.Outcome is OverrideCoverOutcome.WrittenDownloadedJpeg or OverrideCoverOutcome.WrittenConvertedJpeg);
		int alreadyExistsCount = results.Count(static result => result.Outcome == OverrideCoverOutcome.AlreadyExists);

		Assert.Equal(1, writtenCount);
		Assert.Equal(1, alreadyExistsCount);
		Assert.True(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "cover.jpg")));
	}

	/// <summary>
	/// Verifies HTTP non-success responses are reported as download failures.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Failure_ShouldReturnDownloadFailed_WhenHttpStatusIsNonSuccess()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.BadGateway, Encoding.UTF8.GetBytes("gateway"), "text/plain"));
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(
			new OverrideCoverRequest(
				preferredOverrideDirectoryPath,
				[preferredOverrideDirectoryPath],
				"covers/sample.jpg"));

		Assert.Equal(OverrideCoverOutcome.DownloadFailed, result.Outcome);
		Assert.False(result.CoverJpgExists);
		Assert.Contains("HTTP failure", result.Diagnostic, StringComparison.Ordinal);
		Assert.False(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "cover.jpg")));
	}

	/// <summary>
	/// Verifies absolute cover keys using unsupported schemes are classified as deterministic download failures.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Failure_ShouldReturnDownloadFailed_WhenAbsoluteCoverKeyUsesUnsupportedScheme()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");

		ThrowingHttpMessageHandler handler = new();
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(
			new OverrideCoverRequest(
				preferredOverrideDirectoryPath,
				[preferredOverrideDirectoryPath],
				"ftp://invalid.example.test/cover.jpg"));

		Assert.Equal(OverrideCoverOutcome.DownloadFailed, result.Outcome);
		Assert.Equal(Path.Combine(preferredOverrideDirectoryPath, "cover.jpg"), result.CoverJpgPath);
		Assert.False(result.CoverJpgExists);
		Assert.Null(result.ExistingCoverPath);
		Assert.Null(result.CoverUri);
		Assert.NotNull(result.Diagnostic);
		Assert.Contains("http or https", result.Diagnostic, StringComparison.Ordinal);
		Assert.Equal(0, handler.CallCount);
		Assert.False(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "cover.jpg")));
	}

	/// <summary>
	/// Verifies invalid non-JPEG payloads return unsupported-image failures.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Failure_ShouldReturnUnsupportedImage_WhenPayloadCannotBeDecoded()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, Encoding.UTF8.GetBytes("not an image"), "text/plain"));
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(
			new OverrideCoverRequest(
				preferredOverrideDirectoryPath,
				[preferredOverrideDirectoryPath],
				"covers/sample.bin"));

		Assert.Equal(OverrideCoverOutcome.UnsupportedImage, result.Outcome);
		Assert.False(result.CoverJpgExists);
		Assert.Contains("Cover conversion failure", result.Diagnostic, StringComparison.Ordinal);
		Assert.False(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "cover.jpg")));
	}

	/// <summary>
	/// Verifies null requests are rejected.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Exception_ShouldThrow_WhenRequestIsNull()
	{
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, _onePixelJpegPayload, "image/jpeg"));
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		await Assert.ThrowsAsync<ArgumentNullException>(() => service.EnsureCoverJpgAsync(null!));
	}

	/// <summary>
	/// Verifies request construction rejects invalid arguments.
	/// </summary>
	[Fact]
	public void OverrideCoverRequest_Exception_ShouldThrow_WhenArgumentsInvalid()
	{
		Assert.ThrowsAny<ArgumentException>(
			() => new OverrideCoverRequest(
				"",
				["/override"],
				"cover.jpg"));

		Assert.ThrowsAny<ArgumentException>(
			() => new OverrideCoverRequest(
				"/override",
				[],
				"cover.jpg"));

		Assert.ThrowsAny<ArgumentException>(
			() => new OverrideCoverRequest(
				"/override",
				["/override"],
				""));
	}

	/// <summary>
	/// Creates one response with deterministic status, body, and media type.
	/// </summary>
	/// <param name="statusCode">Response status code.</param>
	/// <param name="payload">Response payload bytes.</param>
	/// <param name="mediaType">Response media type.</param>
	/// <returns>Configured response instance.</returns>
	private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, byte[] payload, string mediaType)
	{
		return new HttpResponseMessage(statusCode)
		{
			Content = new ByteArrayContent(payload)
			{
				Headers =
				{
					ContentType = new(mediaType)
				}
			}
		};
	}

	/// <summary>
	/// Creates a directory under one root using path segments.
	/// </summary>
	/// <param name="rootPath">Base directory path.</param>
	/// <param name="segments">Path segments appended under root.</param>
	/// <returns>Created directory path.</returns>
	private static string CreateDirectory(string rootPath, params string[] segments)
	{
		string path = rootPath;
		for (int index = 0; index < segments.Length; index++)
		{
			path = Path.Combine(path, segments[index]);
		}

		return Directory.CreateDirectory(path).FullName;
	}

	/// <summary>
	/// Recording HTTP handler used for deterministic assertions.
	/// </summary>
	private sealed class RecordingHttpMessageHandler : HttpMessageHandler
	{
		/// <summary>
		/// Response callback.
		/// </summary>
		private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;

		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingHttpMessageHandler"/> class.
		/// </summary>
		/// <param name="send">Response callback.</param>
		public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
		{
			_send = send ?? throw new ArgumentNullException(nameof(send));
		}

		/// <summary>
		/// Gets most recent request.
		/// </summary>
		public HttpRequestMessage? LastRequest
		{
			get;
			private set;
		}

		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			LastRequest = request;
			return Task.FromResult(_send(request));
		}
	}

	/// <summary>
	/// HTTP handler that throws if it is ever invoked.
	/// </summary>
	private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
	{
		/// <summary>
		/// Gets invocation count.
		/// </summary>
		public int CallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			CallCount++;
			throw new InvalidOperationException("HTTP request should not execute for already-existing cover scenarios.");
		}
	}

	/// <summary>
	/// HTTP handler that blocks responses until all expected concurrent calls arrive.
	/// </summary>
	private sealed class ConcurrentGateHttpMessageHandler : HttpMessageHandler
	{
		/// <summary>
		/// Response payload bytes.
		/// </summary>
		private readonly byte[] _payload;

		/// <summary>
		/// Expected concurrent call count.
		/// </summary>
		private readonly int _expectedCallCount;

		/// <summary>
		/// Response-release gate.
		/// </summary>
		private readonly TaskCompletionSource _releaseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

		/// <summary>
		/// Arrival gate indicating all expected calls have been observed.
		/// </summary>
		private readonly TaskCompletionSource _arrivalGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

		/// <summary>
		/// Initializes a new instance of the <see cref="ConcurrentGateHttpMessageHandler"/> class.
		/// </summary>
		/// <param name="payload">Response payload bytes.</param>
		/// <param name="expectedCallCount">Expected concurrent call count.</param>
		public ConcurrentGateHttpMessageHandler(byte[] payload, int expectedCallCount)
		{
			_payload = payload ?? throw new ArgumentNullException(nameof(payload));
			_expectedCallCount = expectedCallCount;
		}

		/// <summary>
		/// Gets current call count.
		/// </summary>
		public int CallCount
		{
			get;
			private set;
		}

		/// <summary>
		/// Waits until expected concurrent calls are observed.
		/// </summary>
		/// <returns>Awaitable completion task.</returns>
		public async Task WaitUntilExpectedCallsAsync()
		{
			await _arrivalGate.Task.ConfigureAwait(false);
		}

		/// <summary>
		/// Releases blocked responses.
		/// </summary>
		public void ReleaseResponses()
		{
			_releaseGate.TrySetResult();
		}

		/// <inheritdoc />
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			CallCount++;
			if (CallCount >= _expectedCallCount)
			{
				_arrivalGate.TrySetResult();
			}

			await _releaseGate.Task.ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			return CreateResponse(HttpStatusCode.OK, _payload, "image/jpeg");
		}
	}
}
