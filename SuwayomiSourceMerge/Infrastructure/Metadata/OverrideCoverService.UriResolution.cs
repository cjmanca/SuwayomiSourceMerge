using System.IO;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Provides URI resolution and directory setup helpers for <see cref="OverrideCoverService"/>.
/// </summary>
internal sealed partial class OverrideCoverService
{
	/// <summary>
	/// Resolves one cover URI from the Comick cover-key value.
	/// </summary>
	/// <param name="coverKey">Comick cover key from <c>b2key</c>.</param>
	/// <returns>Resolve outcome tuple.</returns>
	private (bool Success, Uri? CoverUri, string Diagnostic) TryResolveCoverUri(string coverKey)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(coverKey);

		string trimmed = coverKey.Trim();
		if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? absoluteUri))
		{
			if (!string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
			{
				return (false, null, "Cover key absolute URI must use http or https.");
			}

			return (true, absoluteUri, "Success.");
		}

		if (Uri.TryCreate(_coverBaseUri, trimmed.TrimStart('/'), out Uri? resolvedRelativeUri) && resolvedRelativeUri is not null)
		{
			return (true, resolvedRelativeUri, "Success.");
		}

		return (false, null, "Cover key could not be resolved to a valid URI.");
	}

	/// <summary>
	/// Attempts to ensure the preferred override directory exists.
	/// </summary>
	/// <param name="preferredOverrideDirectoryPath">Preferred override directory path.</param>
	/// <returns>Directory setup outcome tuple.</returns>
	private (bool Success, string Diagnostic) TryEnsurePreferredOverrideDirectoryExists(string preferredOverrideDirectoryPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(preferredOverrideDirectoryPath);

		try
		{
			_fileOperations.CreateDirectory(preferredOverrideDirectoryPath);
			return (true, "Success.");
		}
		catch (Exception exception) when (
			exception is IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException)
		{
			string failureKind = exception switch
			{
				IOException => "I/O",
				UnauthorizedAccessException => "permission",
				_ => "path"
			};
			return (false, $"Cover write setup {failureKind} failure: {exception.Message}");
		}
	}

	/// <summary>
	/// Normalizes cover-base URI semantics to absolute <c>http/https</c> and exactly one trailing slash.
	/// </summary>
	/// <param name="coverBaseUri">Base URI value.</param>
	/// <returns>Normalized base URI.</returns>
	private static Uri NormalizeCoverBaseUri(Uri coverBaseUri)
	{
		ArgumentNullException.ThrowIfNull(coverBaseUri);
		if (!coverBaseUri.IsAbsoluteUri)
		{
			throw new ArgumentException("Cover base URI must be absolute.", nameof(coverBaseUri));
		}

		if (!string.Equals(coverBaseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(coverBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("Cover base URI must use http or https.", nameof(coverBaseUri));
		}

		return new Uri(coverBaseUri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
	}
}
