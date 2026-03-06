using HtmlAgilityPack;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// FlareSolverr upstream-response normalization helpers for <see cref="CloudflareAwareComickGateway"/>.
/// </summary>
internal sealed partial class CloudflareAwareComickGateway
{
	/// <summary>
	/// UTF BOM character prefix that may appear at the start of upstream JSON text.
	/// </summary>
	private const char UtfBomCharacter = '\uFEFF';

	/// <summary>
	/// Normalizes one FlareSolverr upstream response into parseable JSON text when possible.
	/// </summary>
	/// <param name="upstreamResponseBody">Raw upstream response body extracted from FlareSolverr wrapper payload.</param>
	/// <returns>Normalization result with normalized JSON text when successful.</returns>
	private ResponseNormalizationResult NormalizeFlaresolverrUpstreamResponse(string? upstreamResponseBody)
	{
		if (string.IsNullOrWhiteSpace(upstreamResponseBody))
		{
			return ResponseNormalizationResult.Failed(
				ResponseNormalizationMode.Failed,
				"FlareSolverr upstream response body was empty.",
				upstreamResponseBody,
				htmlWrapperDetection: HtmlWrapperDetectionState.NotDetected);
		}

		string normalizedBody = NormalizeJsonCandidate(upstreamResponseBody);
		if (!string.IsNullOrWhiteSpace(normalizedBody) && IsJsonStartCharacter(normalizedBody[0]))
		{
			return ResponseNormalizationResult.Succeeded(
				ResponseNormalizationMode.RawJson,
				normalizedBody,
				upstreamResponseBody,
				htmlWrapperDetection: HtmlWrapperDetectionState.NotDetected);
		}

		if (!TryExtractJsonPreContent(
			upstreamResponseBody,
			out string? normalizedPreContent,
			out HtmlWrapperDetectionState htmlWrapperDetection,
			out string extractionDiagnostic))
		{
			return ResponseNormalizationResult.Failed(
				ResponseNormalizationMode.Failed,
				extractionDiagnostic,
				upstreamResponseBody,
				htmlWrapperDetection);
		}

		return ResponseNormalizationResult.Succeeded(
			ResponseNormalizationMode.HtmlPreExtracted,
			normalizedPreContent!,
			upstreamResponseBody,
			htmlWrapperDetection);
	}

	/// <summary>
	/// Attempts to extract one JSON-root-compatible HTML <c>&lt;pre&gt;</c> payload body from one upstream response.
	/// </summary>
	/// <param name="upstreamResponseBody">Raw upstream response text.</param>
	/// <param name="preContent">Extracted normalized preformatted content when successful.</param>
	/// <param name="htmlWrapperDetection">Wrapper-detection state for diagnostics.</param>
	/// <param name="diagnostic">Failure diagnostic when extraction fails.</param>
	/// <returns><see langword="true"/> when extraction succeeds; otherwise <see langword="false"/>.</returns>
	private bool TryExtractJsonPreContent(
		string upstreamResponseBody,
		out string? preContent,
		out HtmlWrapperDetectionState htmlWrapperDetection,
		out string diagnostic)
	{
		preContent = null;
		htmlWrapperDetection = HtmlWrapperDetectionState.NotDetected;
		HtmlNodeCollection? preNodes;
		try
		{
			preNodes = _preNodeSelector(upstreamResponseBody);
		}
		catch (Exception ex) when (!IsFatalException(ex))
		{
			htmlWrapperDetection = HtmlWrapperDetectionState.Unknown;
			diagnostic = $"FlareSolverr HTML-wrapped response could not be parsed: {ex.GetType().Name}.";
			return false;
		}

		if (preNodes is null || preNodes.Count == 0)
		{
			diagnostic = "FlareSolverr upstream response was not JSON and did not contain an HTML <pre> wrapper.";
			return false;
		}

		htmlWrapperDetection = HtmlWrapperDetectionState.Detected;
		foreach (HtmlNode preNode in preNodes)
		{
			string decodedPreContent = HtmlEntity.DeEntitize(preNode.InnerHtml ?? string.Empty);
			string normalizedPreContent = NormalizeJsonCandidate(decodedPreContent);
			if (!string.IsNullOrWhiteSpace(normalizedPreContent) && IsJsonStartCharacter(normalizedPreContent[0]))
			{
				preContent = normalizedPreContent;
				diagnostic = "Success.";
				return true;
			}
		}

		diagnostic = "FlareSolverr HTML-wrapped response contained <pre> blocks but none began with a JSON root token.";
		return false;
	}

	/// <summary>
	/// Selects all HTML <c>&lt;pre&gt;</c> nodes from one upstream payload.
	/// </summary>
	/// <param name="upstreamResponseBody">Raw upstream response text.</param>
	/// <returns>All preformatted nodes in document order, or <see langword="null"/> when unavailable.</returns>
	private static HtmlNodeCollection? SelectPreNodesFromHtml(string upstreamResponseBody)
	{
		ArgumentNullException.ThrowIfNull(upstreamResponseBody);
		HtmlDocument htmlDocument = new();
		htmlDocument.LoadHtml(upstreamResponseBody);
		return htmlDocument.DocumentNode.SelectNodes("//pre");
	}

	/// <summary>
	/// Normalizes one candidate payload by trimming outer whitespace and one optional UTF BOM prefix.
	/// </summary>
	/// <param name="candidatePayload">Candidate payload text.</param>
	/// <returns>Normalized candidate payload text.</returns>
	private static string NormalizeJsonCandidate(string candidatePayload)
	{
		ArgumentNullException.ThrowIfNull(candidatePayload);
		string trimmedCandidate = candidatePayload.Trim();
		if (!string.IsNullOrEmpty(trimmedCandidate) && trimmedCandidate[0] == UtfBomCharacter)
		{
			trimmedCandidate = trimmedCandidate[1..];
		}

		return trimmedCandidate.TrimStart();
	}

	/// <summary>
	/// Determines whether one leading character is valid for a JSON object/array root.
	/// </summary>
	/// <param name="value">Leading character.</param>
	/// <returns><see langword="true"/> when the character is a JSON root token; otherwise <see langword="false"/>.</returns>
	private static bool IsJsonStartCharacter(char value)
	{
		return value == '{' || value == '[';
	}

	/// <summary>
	/// Represents normalization path options for one FlareSolverr upstream response.
	/// </summary>
	private enum ResponseNormalizationMode
	{
		/// <summary>
		/// Upstream body was already JSON and required no extraction.
		/// </summary>
		RawJson = 0,

		/// <summary>
		/// Upstream body was HTML-wrapped and JSON was extracted from a preformatted container.
		/// </summary>
		HtmlPreExtracted = 1,

		/// <summary>
		/// Upstream body could not be normalized into parseable JSON.
		/// </summary>
		Failed = 2
	}

	/// <summary>
	/// Represents wrapper-detection confidence for one normalized upstream response.
	/// </summary>
	private enum HtmlWrapperDetectionState
	{
		/// <summary>
		/// No HTML wrapper was detected.
		/// </summary>
		NotDetected = 0,

		/// <summary>
		/// HTML wrapper presence was confirmed.
		/// </summary>
		Detected = 1,

		/// <summary>
		/// Wrapper detection was inconclusive due to parser failure.
		/// </summary>
		Unknown = 2
	}

	/// <summary>
	/// Represents one response-normalization outcome.
	/// </summary>
	/// <param name="Success">Indicates whether normalization succeeded.</param>
	/// <param name="Mode">Normalization mode.</param>
	/// <param name="NormalizedBody">Normalized JSON text when successful.</param>
	/// <param name="Diagnostic">Deterministic diagnostic text.</param>
	/// <param name="ResponsePrefix">Short response-prefix sample for debug diagnostics.</param>
	/// <param name="HtmlWrapperDetection">Indicates HTML-wrapper detection state for diagnostics.</param>
	private readonly record struct ResponseNormalizationResult(
		bool Success,
		ResponseNormalizationMode Mode,
		string? NormalizedBody,
		string Diagnostic,
		string ResponsePrefix,
		HtmlWrapperDetectionState HtmlWrapperDetection)
	{
		/// <summary>
		/// Maximum response-prefix length captured for diagnostics.
		/// Keeps logs concise while retaining enough leading payload context to classify malformed wrappers.
		/// </summary>
		private const int ResponsePrefixLength = 120;

		/// <summary>
		/// Creates one successful normalization result.
		/// </summary>
		/// <param name="mode">Normalization mode.</param>
		/// <param name="normalizedBody">Normalized JSON text.</param>
		/// <param name="rawBody">Raw response text used for prefix sampling.</param>
		/// <param name="htmlWrapperDetection">HTML-wrapper detection state.</param>
		/// <returns>Successful normalization result.</returns>
		public static ResponseNormalizationResult Succeeded(
			ResponseNormalizationMode mode,
			string normalizedBody,
			string? rawBody,
			HtmlWrapperDetectionState htmlWrapperDetection)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(normalizedBody);
			return new ResponseNormalizationResult(
				Success: true,
				mode,
				normalizedBody,
				Diagnostic: "Success.",
				CreateResponsePrefix(rawBody),
				htmlWrapperDetection);
		}

		/// <summary>
		/// Creates one failed normalization result.
		/// </summary>
		/// <param name="mode">Normalization mode.</param>
		/// <param name="diagnostic">Failure diagnostic text.</param>
		/// <param name="rawBody">Raw response text used for prefix sampling.</param>
		/// <param name="htmlWrapperDetection">HTML-wrapper detection state.</param>
		/// <returns>Failed normalization result.</returns>
		public static ResponseNormalizationResult Failed(
			ResponseNormalizationMode mode,
			string diagnostic,
			string? rawBody,
			HtmlWrapperDetectionState htmlWrapperDetection)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
			return new ResponseNormalizationResult(
				Success: false,
				mode,
				NormalizedBody: null,
				diagnostic,
				CreateResponsePrefix(rawBody),
				htmlWrapperDetection);
		}

		/// <summary>
		/// Creates one deterministic response-prefix sample.
		/// </summary>
		/// <param name="rawBody">Raw response text.</param>
		/// <returns>Prefix sample, or empty when unavailable.</returns>
		private static string CreateResponsePrefix(string? rawBody)
		{
			if (string.IsNullOrEmpty(rawBody))
			{
				return string.Empty;
			}

			string normalizedWhitespace = rawBody
				.Replace("\r", " ", StringComparison.Ordinal)
				.Replace("\n", " ", StringComparison.Ordinal)
				.Trim();
			if (normalizedWhitespace.Length <= ResponsePrefixLength)
			{
				return normalizedWhitespace;
			}

			return normalizedWhitespace[..ResponsePrefixLength];
		}
	}
}
