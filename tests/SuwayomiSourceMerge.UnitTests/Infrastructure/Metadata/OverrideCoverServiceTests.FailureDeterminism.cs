namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.IO;
using System.Net;
using System.Net.Http;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies deterministic failure mapping behavior for <see cref="OverrideCoverService"/>.
/// </summary>
public sealed partial class OverrideCoverServiceTests
{
	/// <summary>
	/// Verifies malformed absolute-looking cover keys return deterministic download-failed outcomes.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Failure_ShouldReturnDownloadFailed_WhenCoverKeyCannotBeComposedIntoValidUri()
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
				"http://"));

		Assert.Equal(OverrideCoverOutcome.DownloadFailed, result.Outcome);
		Assert.Equal(Path.Combine(preferredOverrideDirectoryPath, "cover.jpg"), result.CoverJpgPath);
		Assert.False(result.CoverJpgExists);
		Assert.Null(result.ExistingCoverPath);
		Assert.Null(result.CoverUri);
		Assert.NotNull(result.Diagnostic);
		Assert.Contains("valid URI", result.Diagnostic, StringComparison.Ordinal);
		Assert.Equal(0, handler.CallCount);
		Assert.False(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "cover.jpg")));
	}

	/// <summary>
	/// Verifies preferred-directory setup failures map to write-failed outcomes instead of throwing.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Failure_ShouldReturnWriteFailed_WhenPreferredOverrideDirectoryCannotBeCreated()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string overrideRootPath = CreateDirectory(temporaryDirectory.Path, "override");
		string blockedPreferredPath = Path.Combine(overrideRootPath, "blocked-preferred");
		File.WriteAllBytes(blockedPreferredPath, _onePixelJpegPayload);

		ThrowingHttpMessageHandler handler = new();
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(
			new OverrideCoverRequest(
				blockedPreferredPath,
				[blockedPreferredPath],
				"covers/sample.jpg"));

		Assert.Equal(OverrideCoverOutcome.WriteFailed, result.Outcome);
		Assert.Equal(Path.Combine(blockedPreferredPath, "cover.jpg"), result.CoverJpgPath);
		Assert.False(result.CoverJpgExists);
		Assert.Null(result.ExistingCoverPath);
		Assert.Null(result.CoverUri);
		Assert.NotNull(result.Diagnostic);
		Assert.Contains("setup", result.Diagnostic, StringComparison.Ordinal);
		Assert.Equal(0, handler.CallCount);
	}

	/// <summary>
	/// Verifies write-stage NotSupported exceptions map to write-failed outcomes instead of throwing.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Failure_ShouldReturnWriteFailed_WhenWriteStageThrowsNotSupportedException()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, _onePixelJpegPayload, "image/jpeg"));
		using HttpClient httpClient = new(handler);
		FaultInjectingCoverFileOperations fileOperations = new(
			writeOverride: static (_, _) => throw new NotSupportedException("simulated write-stage not-supported failure"));
		using OverrideCoverService service = new(httpClient, coverBaseUri: null, fileOperations);

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(
			new OverrideCoverRequest(
				preferredOverrideDirectoryPath,
				[preferredOverrideDirectoryPath],
				"covers/sample.jpg"));

		Assert.Equal(OverrideCoverOutcome.WriteFailed, result.Outcome);
		Assert.False(result.CoverJpgExists);
		Assert.NotNull(result.Diagnostic);
		Assert.Contains("path failure", result.Diagnostic, StringComparison.Ordinal);
		Assert.False(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "cover.jpg")));
	}

	/// <summary>
	/// Verifies write-stage PathTooLong exceptions map to write-failed outcomes instead of throwing.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Failure_ShouldReturnWriteFailed_WhenWriteStageThrowsPathTooLongException()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, _onePixelJpegPayload, "image/jpeg"));
		using HttpClient httpClient = new(handler);
		FaultInjectingCoverFileOperations fileOperations = new(
			moveOverride: static (_, _, _) => throw new PathTooLongException("simulated write-stage path-too-long failure"));
		using OverrideCoverService service = new(httpClient, coverBaseUri: null, fileOperations);

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(
			new OverrideCoverRequest(
				preferredOverrideDirectoryPath,
				[preferredOverrideDirectoryPath],
				"covers/sample.jpg"));

		Assert.Equal(OverrideCoverOutcome.WriteFailed, result.Outcome);
		Assert.False(result.CoverJpgExists);
		Assert.NotNull(result.Diagnostic);
		Assert.Contains("path failure", result.Diagnostic, StringComparison.Ordinal);
		Assert.False(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "cover.jpg")));
	}

	/// <summary>
	/// Verifies oversized payloads are rejected deterministically before full content materialization.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Failure_ShouldReturnDownloadFailed_WhenContentLengthExceedsMaximumSize()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		const long oversizedBytes = (32L * 1024L * 1024L) + 1L;

		RecordingHttpMessageHandler handler = new(
			_ => new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new OversizedContentLengthHttpContent(oversizedBytes, "image/jpeg")
			});
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(
			new OverrideCoverRequest(
				preferredOverrideDirectoryPath,
				[preferredOverrideDirectoryPath],
				"covers/sample.jpg"));

		Assert.Equal(OverrideCoverOutcome.DownloadFailed, result.Outcome);
		Assert.False(result.CoverJpgExists);
		Assert.NotNull(result.Diagnostic);
		Assert.Contains("maximum size", result.Diagnostic, StringComparison.Ordinal);
		Assert.False(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "cover.jpg")));
	}

	/// <summary>
	/// Verifies payloads matching JPEG magic bytes but failing decode validation map to unsupported-image outcomes.
	/// </summary>
	[Fact]
	public async Task EnsureCoverJpgAsync_Failure_ShouldReturnUnsupportedImage_WhenJpegSignaturePayloadFailsDecodeValidation()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		byte[] invalidJpegSignaturePayload =
		[
			0xFF,
			0xD8,
			0xFF,
			0x00,
			0x10,
			0x20,
			0x30,
		];

		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, invalidJpegSignaturePayload, "image/jpeg"));
		using HttpClient httpClient = new(handler);
		using OverrideCoverService service = new(httpClient, coverBaseUri: null);

		OverrideCoverResult result = await service.EnsureCoverJpgAsync(
			new OverrideCoverRequest(
				preferredOverrideDirectoryPath,
				[preferredOverrideDirectoryPath],
				"covers/sample.jpg"));

		Assert.Equal(OverrideCoverOutcome.UnsupportedImage, result.Outcome);
		Assert.False(result.CoverJpgExists);
		Assert.NotNull(result.Diagnostic);
		Assert.Contains("JPEG validation failure", result.Diagnostic, StringComparison.Ordinal);
		Assert.False(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "cover.jpg")));
	}

	/// <summary>
	/// File-operation shim that allows injecting write-stage faults while using physical filesystem behavior otherwise.
	/// </summary>
	private sealed class FaultInjectingCoverFileOperations : IOverrideCoverFileOperations
	{
		/// <summary>
		/// Physical filesystem adapter.
		/// </summary>
		private readonly OverrideCoverPhysicalFileOperations _inner = new();

		/// <summary>
		/// Optional write interception callback.
		/// </summary>
		private readonly Action<string, byte[]>? _writeOverride;

		/// <summary>
		/// Optional move interception callback.
		/// </summary>
		private readonly Action<string, string, bool>? _moveOverride;

		/// <summary>
		/// Initializes a new instance of the <see cref="FaultInjectingCoverFileOperations"/> class.
		/// </summary>
		/// <param name="writeOverride">Optional write interception callback.</param>
		/// <param name="moveOverride">Optional move interception callback.</param>
		public FaultInjectingCoverFileOperations(
			Action<string, byte[]>? writeOverride = null,
			Action<string, string, bool>? moveOverride = null)
		{
			_writeOverride = writeOverride;
			_moveOverride = moveOverride;
		}

		/// <inheritdoc />
		public void CreateDirectory(string path)
		{
			_inner.CreateDirectory(path);
		}

		/// <inheritdoc />
		public void WriteAllBytes(string path, byte[] bytes)
		{
			if (_writeOverride is not null)
			{
				_writeOverride(path, bytes);
				return;
			}

			_inner.WriteAllBytes(path, bytes);
		}

		/// <inheritdoc />
		public void MoveFile(string sourcePath, string destinationPath, bool overwrite)
		{
			if (_moveOverride is not null)
			{
				_moveOverride(sourcePath, destinationPath, overwrite);
				return;
			}

			_inner.MoveFile(sourcePath, destinationPath, overwrite);
		}

		/// <inheritdoc />
		public bool FileExists(string path)
		{
			return _inner.FileExists(path);
		}

		/// <inheritdoc />
		public void DeleteFile(string path)
		{
			_inner.DeleteFile(path);
		}
	}

	/// <summary>
	/// HTTP content shim that reports an oversized content length and throws if stream reads are attempted.
	/// </summary>
	private sealed class OversizedContentLengthHttpContent : HttpContent
	{
		/// <summary>
		/// Backing content-length value.
		/// </summary>
		private readonly long _contentLength;

		/// <summary>
		/// Initializes a new instance of the <see cref="OversizedContentLengthHttpContent"/> class.
		/// </summary>
		/// <param name="contentLength">Content length value to report.</param>
		/// <param name="mediaType">Media type header value.</param>
		public OversizedContentLengthHttpContent(long contentLength, string mediaType)
		{
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contentLength);
			ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

			_contentLength = contentLength;
			Headers.ContentLength = contentLength;
			Headers.ContentType = new(mediaType);
		}

		/// <inheritdoc />
		protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
		{
			throw new InvalidOperationException("Oversized-content fixture should not be serialized.");
		}

		/// <inheritdoc />
		protected override bool TryComputeLength(out long length)
		{
			length = _contentLength;
			return true;
		}

		/// <inheritdoc />
		protected override Task<Stream> CreateContentReadStreamAsync()
		{
			throw new InvalidOperationException("Oversized-content fixture should not be read as a stream.");
		}
	}
}
