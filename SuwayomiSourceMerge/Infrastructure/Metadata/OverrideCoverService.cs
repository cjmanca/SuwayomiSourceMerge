using System.Buffers;
using System.IO;
using System.Net.Http.Headers;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Ensures <c>cover.jpg</c> metadata exists using override-exists checks and Comick cover payload download/conversion,
/// with deterministic outcome-based failure mapping for malformed URI inputs and write/setup failures.
/// </summary>
internal sealed partial class OverrideCoverService : IOverrideCoverService, IDisposable
{
	/// <summary>
	/// Canonical cover file name.
	/// </summary>
	private const string CoverJpgFileName = "cover.jpg";

	/// <summary>
	/// Canonical Comick cover base URI used for relative <c>b2key</c> values.
	/// </summary>
	private const string DefaultComickCoverBaseUri = "https://meo.comick.pictures/";

	/// <summary>
	/// Maximum supported cover-download size in bytes (32 MiB).
	/// </summary>
	private const long MaxCoverDownloadBytes = 32L * 1024L * 1024L;

	/// <summary>
	/// Buffered stream-read chunk size used for bounded cover downloads.
	/// </summary>
	private const int DownloadBufferSizeBytes = 81920;

	/// <summary>
	/// Default request timeout used for internally-owned HTTP clients.
	/// </summary>
	private static readonly TimeSpan _defaultRequestTimeout = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Shared JPEG encoder used for conversion output.
	/// </summary>
	private static readonly JpegEncoder _jpegEncoder = new()
	{
		Quality = 90
	};

	/// <summary>
	/// HTTP client dependency.
	/// </summary>
	private readonly HttpClient _httpClient;

	/// <summary>
	/// Indicates whether this instance owns and must dispose the HTTP client.
	/// </summary>
	private readonly bool _ownsHttpClient;

	/// <summary>
	/// Base URI used to resolve relative Comick cover keys.
	/// </summary>
	private readonly Uri _coverBaseUri;

	/// <summary>
	/// File operation dependency used for deterministic write-path behavior and testability.
	/// </summary>
	private readonly IOverrideCoverFileOperations _fileOperations;

	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideCoverService"/> class using default runtime dependencies.
	/// </summary>
	public OverrideCoverService()
		: this(httpClient: null, coverBaseUri: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideCoverService"/> class.
	/// </summary>
	/// <param name="httpClient">
	/// Optional HTTP client.
	/// When <see langword="null"/>, one internal client is created and disposed by this instance.
	/// </param>
	/// <param name="coverBaseUri">Optional cover-base URI used to resolve relative cover keys.</param>
	internal OverrideCoverService(HttpClient? httpClient, Uri? coverBaseUri)
		: this(httpClient, coverBaseUri, fileOperations: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideCoverService"/> class.
	/// </summary>
	/// <param name="httpClient">
	/// Optional HTTP client.
	/// When <see langword="null"/>, one internal client is created and disposed by this instance.
	/// </param>
	/// <param name="coverBaseUri">Optional cover-base URI used to resolve relative cover keys.</param>
	/// <param name="fileOperations">Optional file operation dependency used by setup/write paths.</param>
	internal OverrideCoverService(
		HttpClient? httpClient,
		Uri? coverBaseUri,
		IOverrideCoverFileOperations? fileOperations)
	{
		if (httpClient is null)
		{
			_httpClient = new HttpClient
			{
				Timeout = _defaultRequestTimeout
			};
			_ownsHttpClient = true;
		}
		else
		{
			_httpClient = httpClient;
			_ownsHttpClient = false;
		}

		_coverBaseUri = NormalizeCoverBaseUri(coverBaseUri ?? new Uri(DefaultComickCoverBaseUri, UriKind.Absolute));
		_fileOperations = fileOperations ?? new OverrideCoverPhysicalFileOperations();
	}

	/// <inheritdoc />
	public async Task<OverrideCoverResult> EnsureCoverJpgAsync(
		OverrideCoverRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		string preferredCoverPath = Path.Combine(
			request.PreferredOverrideDirectoryPath,
			CoverJpgFileName);

		if (TryFindExistingOverrideCoverPath(request, out string? existingCoverPath))
		{
			return CreateAlreadyExistsResult(existingCoverPath!, coverUri: null);
		}

		(bool setupSuccess, string setupDiagnostic) = TryEnsurePreferredOverrideDirectoryExists(request.PreferredOverrideDirectoryPath);
		if (!setupSuccess)
		{
			if (TryFindExistingOverrideCoverPath(request, out string? raceExistingCoverPath))
			{
				return CreateAlreadyExistsResult(raceExistingCoverPath!, coverUri: null);
			}

			return new OverrideCoverResult(
				OverrideCoverOutcome.WriteFailed,
				preferredCoverPath,
				coverJpgExists: false,
				existingCoverPath: null,
				coverUri: null,
				diagnostic: setupDiagnostic);
		}

		(bool resolveSuccess, Uri? coverUri, string resolveDiagnostic) = TryResolveCoverUri(request.CoverKey);
		if (!resolveSuccess || coverUri is null)
		{
			if (TryFindExistingOverrideCoverPath(request, out string? raceExistingCoverPath))
			{
				return CreateAlreadyExistsResult(raceExistingCoverPath!, coverUri: null);
			}

			return new OverrideCoverResult(
				OverrideCoverOutcome.DownloadFailed,
				preferredCoverPath,
				coverJpgExists: false,
				existingCoverPath: null,
				coverUri: null,
				diagnostic: resolveDiagnostic);
		}

		(bool downloadSuccess, byte[]? payloadBytes, string? downloadDiagnostic) = await DownloadCoverBytesAsync(
				coverUri,
				cancellationToken)
			.ConfigureAwait(false);
		if (!downloadSuccess || payloadBytes is null)
		{
			if (File.Exists(preferredCoverPath))
			{
				return CreateAlreadyExistsResult(preferredCoverPath, coverUri);
			}

			return new OverrideCoverResult(
				OverrideCoverOutcome.DownloadFailed,
				preferredCoverPath,
				coverJpgExists: false,
				existingCoverPath: null,
				coverUri,
				downloadDiagnostic);
		}

		OverrideCoverOutcome successOutcome;
		byte[] outputBytes;
		if (IsJpegPayload(payloadBytes))
		{
			(bool jpegValidationSuccess, string jpegValidationDiagnostic) = TryValidateJpegPayload(payloadBytes);
			if (!jpegValidationSuccess)
			{
				if (File.Exists(preferredCoverPath))
				{
					return CreateAlreadyExistsResult(preferredCoverPath, coverUri);
				}

				return new OverrideCoverResult(
					OverrideCoverOutcome.UnsupportedImage,
					preferredCoverPath,
					coverJpgExists: false,
					existingCoverPath: null,
					coverUri,
					jpegValidationDiagnostic);
			}

			successOutcome = OverrideCoverOutcome.WrittenDownloadedJpeg;
			outputBytes = payloadBytes;
		}
		else
		{
			(bool conversionSuccess, byte[]? convertedBytes, string? conversionDiagnostic) = TryConvertToJpeg(payloadBytes);
			if (!conversionSuccess || convertedBytes is null)
			{
				if (File.Exists(preferredCoverPath))
				{
					return CreateAlreadyExistsResult(preferredCoverPath, coverUri);
				}

				return new OverrideCoverResult(
					OverrideCoverOutcome.UnsupportedImage,
					preferredCoverPath,
					coverJpgExists: false,
					existingCoverPath: null,
					coverUri,
					conversionDiagnostic);
			}

			successOutcome = OverrideCoverOutcome.WrittenConvertedJpeg;
			outputBytes = convertedBytes;
		}

		(bool writeSuccess, bool destinationAlreadyExists, string? writeDiagnostic) = TryWriteCoverAtomically(
			preferredCoverPath,
			outputBytes);
		if (!writeSuccess)
		{
			if (destinationAlreadyExists || File.Exists(preferredCoverPath))
			{
				return CreateAlreadyExistsResult(preferredCoverPath, coverUri);
			}

			return new OverrideCoverResult(
				OverrideCoverOutcome.WriteFailed,
				preferredCoverPath,
				coverJpgExists: false,
				existingCoverPath: null,
				coverUri,
				writeDiagnostic);
		}

		return new OverrideCoverResult(
			successOutcome,
			preferredCoverPath,
			coverJpgExists: true,
			existingCoverPath: null,
			coverUri,
			diagnostic: null);
	}

	/// <summary>
	/// Creates one standardized <see cref="OverrideCoverOutcome.AlreadyExists"/> result.
	/// </summary>
	/// <param name="existingCoverPath">Existing cover path.</param>
	/// <param name="coverUri">Resolved cover URI when available.</param>
	/// <returns>Already-exists result instance.</returns>
	private static OverrideCoverResult CreateAlreadyExistsResult(string existingCoverPath, Uri? coverUri)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(existingCoverPath);

		return new OverrideCoverResult(
			OverrideCoverOutcome.AlreadyExists,
			existingCoverPath,
			coverJpgExists: true,
			existingCoverPath,
			coverUri,
			diagnostic: null);
	}

	/// <summary>
	/// Attempts to find an existing <c>cover.jpg</c> file in override directories.
	/// </summary>
	/// <param name="request">Ensure request.</param>
	/// <param name="existingCoverPath">Existing cover path when found.</param>
	/// <returns><see langword="true"/> when an existing file is found; otherwise <see langword="false"/>.</returns>
	private static bool TryFindExistingOverrideCoverPath(
		OverrideCoverRequest request,
		out string? existingCoverPath)
	{
		ArgumentNullException.ThrowIfNull(request);

		HashSet<string> checkedDirectories = new(StringComparer.Ordinal);
		if (checkedDirectories.Add(request.PreferredOverrideDirectoryPath))
		{
			string preferredCoverPath = Path.Combine(request.PreferredOverrideDirectoryPath, CoverJpgFileName);
			if (File.Exists(preferredCoverPath))
			{
				existingCoverPath = preferredCoverPath;
				return true;
			}
		}

		for (int index = 0; index < request.AllOverrideDirectoryPaths.Count; index++)
		{
			string overrideDirectoryPath = request.AllOverrideDirectoryPaths[index];
			if (!checkedDirectories.Add(overrideDirectoryPath))
			{
				continue;
			}

			string coverPath = Path.Combine(overrideDirectoryPath, CoverJpgFileName);
			if (File.Exists(coverPath))
			{
				existingCoverPath = coverPath;
				return true;
			}
		}

		existingCoverPath = null;
		return false;
	}

	/// <summary>
	/// Downloads cover payload bytes from one resolved URI.
	/// </summary>
	/// <param name="coverUri">Resolved cover URI.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Download outcome tuple.</returns>
	private async Task<(bool Success, byte[]? PayloadBytes, string Diagnostic)> DownloadCoverBytesAsync(
		Uri coverUri,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(coverUri);

		using HttpRequestMessage request = new(HttpMethod.Get, coverUri);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));

		try
		{
			using HttpResponseMessage response = await _httpClient
				.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
				.ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				return (false, null, $"Cover download HTTP failure status code: {(int)response.StatusCode}.");
			}

			long? contentLength = response.Content.Headers.ContentLength;
			if (contentLength.HasValue && contentLength.Value > MaxCoverDownloadBytes)
			{
				return (false, null, $"Cover download exceeded maximum size of {MaxCoverDownloadBytes} bytes.");
			}

			await using Stream payloadStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
			using MemoryStream payloadBuffer = new();
			byte[] chunkBuffer = ArrayPool<byte>.Shared.Rent(DownloadBufferSizeBytes);
			long totalBytesRead = 0;
			try
			{
				while (true)
				{
					int bytesRead = await payloadStream
						.ReadAsync(chunkBuffer.AsMemory(0, chunkBuffer.Length), cancellationToken)
						.ConfigureAwait(false);
					if (bytesRead == 0)
					{
						break;
					}

					totalBytesRead += bytesRead;
					if (totalBytesRead > MaxCoverDownloadBytes)
					{
						return (false, null, $"Cover download exceeded maximum size of {MaxCoverDownloadBytes} bytes.");
					}

					payloadBuffer.Write(chunkBuffer, 0, bytesRead);
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(chunkBuffer);
			}

			byte[] payloadBytes = payloadBuffer.ToArray();
			if (payloadBytes.Length == 0)
			{
				return (false, null, "Cover download returned empty content.");
			}

			return (true, payloadBytes, "Success.");
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (HttpRequestException exception)
		{
			return (false, null, $"Cover download transport failure: {exception.Message}");
		}
		catch (IOException exception)
		{
			return (false, null, $"Cover download I/O failure: {exception.Message}");
		}
		catch (OperationCanceledException exception)
		{
			return (false, null, $"Cover download timeout/cancellation failure: {exception.Message}");
		}
	}

	/// <summary>
	/// Determines whether payload bytes are already in JPEG format by checking JPEG magic bytes.
	/// </summary>
	/// <param name="payloadBytes">Payload bytes.</param>
	/// <returns><see langword="true"/> when payload is JPEG; otherwise <see langword="false"/>.</returns>
	private static bool IsJpegPayload(byte[] payloadBytes)
	{
		ArgumentNullException.ThrowIfNull(payloadBytes);
		return payloadBytes.Length >= 3 &&
			payloadBytes[0] == 0xFF &&
			payloadBytes[1] == 0xD8 &&
			payloadBytes[2] == 0xFF;
	}

	/// <summary>
	/// Attempts to validate one payload advertised as JPEG by decoding it.
	/// </summary>
	/// <param name="payloadBytes">JPEG-signature payload bytes.</param>
	/// <returns>Validation outcome tuple.</returns>
	private static (bool Success, string Diagnostic) TryValidateJpegPayload(byte[] payloadBytes)
	{
		ArgumentNullException.ThrowIfNull(payloadBytes);

		try
		{
			using Image image = Image.Load(payloadBytes);
			return (true, "Success.");
		}
		catch (Exception exception)
		{
			// Intentional broad catch: decode-validation faults must map to deterministic UnsupportedImage outcomes.
			return (false, $"Cover JPEG validation failure: {exception.Message}");
		}
	}

	/// <summary>
	/// Attempts to convert one downloaded payload to JPEG bytes.
	/// </summary>
	/// <param name="payloadBytes">Input payload bytes.</param>
	/// <returns>Conversion outcome tuple.</returns>
	private static (bool Success, byte[]? ConvertedBytes, string Diagnostic) TryConvertToJpeg(byte[] payloadBytes)
	{
		ArgumentNullException.ThrowIfNull(payloadBytes);

		try
		{
			using Image image = Image.Load(payloadBytes);
			using MemoryStream stream = new();
			image.Save(stream, _jpegEncoder);
			byte[] convertedBytes = stream.ToArray();
			if (convertedBytes.Length == 0)
			{
				return (false, null, "Cover conversion produced empty JPEG output.");
			}

			return (true, convertedBytes, "Success.");
		}
		catch (Exception exception)
		{
			// Intentional broad catch: conversion faults are mapped to deterministic UnsupportedImage outcomes.
			return (false, null, $"Cover conversion failure: {exception.Message}");
		}
	}

	/// <summary>
	/// Attempts to write <c>cover.jpg</c> atomically without overwrite.
	/// </summary>
	/// <param name="coverJpgPath">Destination cover path.</param>
	/// <param name="outputBytes">Output bytes to write.</param>
	/// <returns>Write outcome tuple.</returns>
	private (bool Success, bool DestinationAlreadyExists, string? Diagnostic) TryWriteCoverAtomically(
		string coverJpgPath,
		byte[] outputBytes)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(coverJpgPath);
		ArgumentNullException.ThrowIfNull(outputBytes);

		string destinationDirectory = Path.GetDirectoryName(coverJpgPath)
			?? throw new InvalidOperationException("cover.jpg destination directory could not be determined.");

		string temporaryPath = $"{coverJpgPath}.{Guid.NewGuid():N}.tmp";
		try
		{
			_fileOperations.CreateDirectory(destinationDirectory);
			_fileOperations.WriteAllBytes(temporaryPath, outputBytes);
			_fileOperations.MoveFile(temporaryPath, coverJpgPath, overwrite: false);
			return (true, false, null);
		}
		catch (PathTooLongException exception)
		{
			bool destinationExists = _fileOperations.FileExists(coverJpgPath);
			return (false, destinationExists, $"Cover write path failure: {exception.Message}");
		}
		catch (NotSupportedException exception)
		{
			bool destinationExists = _fileOperations.FileExists(coverJpgPath);
			return (false, destinationExists, $"Cover write path failure: {exception.Message}");
		}
		catch (UnauthorizedAccessException exception)
		{
			bool destinationExists = _fileOperations.FileExists(coverJpgPath);
			return (false, destinationExists, $"Cover write permission failure: {exception.Message}");
		}
		catch (IOException exception)
		{
			bool destinationExists = _fileOperations.FileExists(coverJpgPath);
			return (false, destinationExists, $"Cover write I/O failure: {exception.Message}");
		}
		finally
		{
			try
			{
				if (_fileOperations.FileExists(temporaryPath))
				{
					_fileOperations.DeleteFile(temporaryPath);
				}
			}
			catch (Exception exception) when (
				exception is IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException)
			{
				// Best-effort temporary cleanup only; ensure outcome is already determined.
			}
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_ownsHttpClient)
		{
			_httpClient.Dispose();
		}
	}
}
