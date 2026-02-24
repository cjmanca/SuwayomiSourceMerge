namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Describes the terminal outcome of a <c>cover.jpg</c> ensure operation.
/// </summary>
internal enum OverrideCoverOutcome
{
	/// <summary>
	/// A <c>cover.jpg</c> file already existed in an override directory, so no action was taken.
	/// </summary>
	AlreadyExists = 0,

	/// <summary>
	/// A JPEG payload was downloaded and written to the preferred override directory.
	/// </summary>
	WrittenDownloadedJpeg = 1,

	/// <summary>
	/// A non-JPEG payload was downloaded, converted to JPEG, and written to the preferred override directory.
	/// </summary>
	WrittenConvertedJpeg = 2,

	/// <summary>
	/// Cover download failed before any file write succeeded.
	/// </summary>
	DownloadFailed = 3,

	/// <summary>
	/// Cover payload could not be decoded as an image for JPEG conversion.
	/// </summary>
	UnsupportedImage = 4,

	/// <summary>
	/// Cover write failed and no destination race-created file was detected.
	/// </summary>
	WriteFailed = 5
}
