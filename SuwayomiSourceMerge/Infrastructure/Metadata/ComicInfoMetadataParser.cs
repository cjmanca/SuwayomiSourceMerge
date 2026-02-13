using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Parses ComicInfo.xml metadata using strict XML parsing first and line-scanner fallback for malformed files.
/// </summary>
internal sealed class ComicInfoMetadataParser : IComicInfoMetadataParser
{

	/// <summary>
	/// ComicInfo series element local name.
	/// </summary>
	private const string SERIES_ELEMENT_NAME = "Series";

	/// <summary>
	/// ComicInfo writer element local name.
	/// </summary>
	private const string WRITER_ELEMENT_NAME = "Writer";

	/// <summary>
	/// ComicInfo penciller element local name.
	/// </summary>
	private const string PENCILLER_ELEMENT_NAME = "Penciller";

	/// <summary>
	/// ComicInfo summary element local name.
	/// </summary>
	private const string SUMMARY_ELEMENT_NAME = "Summary";

	/// <summary>
	/// ComicInfo genre element local name.
	/// </summary>
	private const string GENRE_ELEMENT_NAME = "Genre";

	/// <summary>
	/// ComicInfo status element local name.
	/// </summary>
	private const string STATUS_ELEMENT_NAME = "Status";

	/// <summary>
	/// ComicInfo status fallback element local name.
	/// </summary>
	private const string TACHIYOMI_STATUS_ELEMENT_NAME = "PublishingStatusTachiyomi";

	/// <inheritdoc />
	public bool TryParse(string comicInfoXmlPath, out ComicInfoMetadata? metadata)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(comicInfoXmlPath);

		metadata = null;
		if (!File.Exists(comicInfoXmlPath))
		{
			return false;
		}

		string xmlContent = File.ReadAllText(comicInfoXmlPath);
		if (string.IsNullOrWhiteSpace(xmlContent))
		{
			return false;
		}

		if (TryParseStrict(xmlContent, out metadata))
		{
			return true;
		}

		return TryParseFallback(xmlContent, out metadata);
	}

	/// <summary>
	/// Attempts strict XML parsing using <see cref="XDocument"/>.
	/// </summary>
	/// <param name="xmlContent">ComicInfo.xml text content.</param>
	/// <param name="metadata">Parsed metadata when successful.</param>
	/// <returns><see langword="true"/> when strict parsing succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryParseStrict(string xmlContent, out ComicInfoMetadata? metadata)
	{
		metadata = null;

		try
		{
			XDocument document = XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);

			string series = ReadFirstElementValue(document, SERIES_ELEMENT_NAME);
			string writer = ReadFirstElementValue(document, WRITER_ELEMENT_NAME);
			string penciller = ReadFirstElementValue(document, PENCILLER_ELEMENT_NAME);
			string summary = ReadFirstElementValue(document, SUMMARY_ELEMENT_NAME);
			string genre = ReadFirstElementValue(document, GENRE_ELEMENT_NAME);
			string status = ReadFirstElementValue(document, STATUS_ELEMENT_NAME);

			if (string.IsNullOrWhiteSpace(status))
			{
				status = ReadFirstElementValue(document, TACHIYOMI_STATUS_ELEMENT_NAME);
			}

			metadata = new ComicInfoMetadata(
				series,
				writer,
				penciller,
				summary,
				genre,
				status);
			return true;
		}
		catch (XmlException)
		{
			return false;
		}
	}

	/// <summary>
	/// Attempts tolerant line-scanner extraction for malformed XML text.
	/// </summary>
	/// <param name="xmlContent">ComicInfo.xml text content.</param>
	/// <param name="metadata">Parsed metadata when successful.</param>
	/// <returns><see langword="true"/> when fallback parsing succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryParseFallback(string xmlContent, out ComicInfoMetadata? metadata)
	{
		metadata = null;

		string series = string.Empty;
		string writer = string.Empty;
		string penciller = string.Empty;
		string summary = string.Empty;
		string genre = string.Empty;
		string status = string.Empty;

		bool foundAnyTag = false;
		bool inSummary = false;
		StringBuilder summaryBuilder = new();

		string[] lines = xmlContent.Split('\n');
		foreach (string rawLine in lines)
		{
			string line = rawLine.Replace("\r", string.Empty, StringComparison.Ordinal);

			if (inSummary)
			{
				if (TryFindClosingTagStart(line, SUMMARY_ELEMENT_NAME, 0, out int summaryCloseTagStart))
				{
					AppendSummaryLine(summaryBuilder, line[..summaryCloseTagStart]);
					inSummary = false;
					summary = summaryBuilder.ToString();
				}
				else
				{
					AppendSummaryLine(summaryBuilder, line);
				}

				continue;
			}

			if (summary.Length == 0
				&& TryReadSummaryStartFromLine(line, out string summaryStartValue, out bool summaryClosed))
			{
				foundAnyTag = true;
				if (summaryClosed)
				{
					summary = summaryStartValue;
				}
				else
				{
					inSummary = true;
					AppendSummaryLine(summaryBuilder, summaryStartValue);
				}

				continue;
			}

			if (series.Length == 0 && TryReadScalarValueFromLine(line, SERIES_ELEMENT_NAME, out series))
			{
				foundAnyTag = true;
			}

			if (writer.Length == 0 && TryReadScalarValueFromLine(line, WRITER_ELEMENT_NAME, out writer))
			{
				foundAnyTag = true;
			}

			if (penciller.Length == 0 && TryReadScalarValueFromLine(line, PENCILLER_ELEMENT_NAME, out penciller))
			{
				foundAnyTag = true;
			}

			if (genre.Length == 0 && TryReadScalarValueFromLine(line, GENRE_ELEMENT_NAME, out genre))
			{
				foundAnyTag = true;
			}

			if (status.Length == 0 && TryReadScalarValueFromLine(line, STATUS_ELEMENT_NAME, out status))
			{
				foundAnyTag = true;
			}
		}

		if (inSummary)
		{
			summary = summaryBuilder.ToString();
		}

		bool foundTachiyomiStatus = false;
		if (string.IsNullOrWhiteSpace(status))
		{
			foreach (string rawLine in lines)
			{
				string line = rawLine.Replace("\r", string.Empty, StringComparison.Ordinal);
				if (!TryReadScalarValueFromLine(line, TACHIYOMI_STATUS_ELEMENT_NAME, out status))
				{
					continue;
				}

				foundTachiyomiStatus = true;
				break;
			}
		}

		if (!foundAnyTag && !foundTachiyomiStatus)
		{
			return false;
		}

		metadata = new ComicInfoMetadata(
			WebUtility.HtmlDecode(series),
			WebUtility.HtmlDecode(writer),
			WebUtility.HtmlDecode(penciller),
			WebUtility.HtmlDecode(summary),
			WebUtility.HtmlDecode(genre),
			WebUtility.HtmlDecode(status));
		return true;
	}

	/// <summary>
	/// Reads the first element value matching a local name from a parsed XML document.
	/// </summary>
	/// <param name="document">Parsed XML document.</param>
	/// <param name="elementName">Case-insensitive local name to locate.</param>
	/// <returns>First matched element value, or an empty string if no element matches.</returns>
	private static string ReadFirstElementValue(XDocument document, string elementName)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentException.ThrowIfNullOrWhiteSpace(elementName);

		XElement? element = document
			.Descendants()
			.FirstOrDefault(
				candidate => string.Equals(
					candidate.Name.LocalName,
					elementName,
					StringComparison.OrdinalIgnoreCase));

		return element?.Value ?? string.Empty;
	}

	/// <summary>
	/// Attempts to read one scalar tag value from a line.
	/// </summary>
	/// <param name="line">One line of XML-like text.</param>
	/// <param name="elementName">Element local name.</param>
	/// <param name="value">Captured value when matching succeeds.</param>
	/// <returns><see langword="true"/> when one tag value is captured; otherwise <see langword="false"/>.</returns>
	private static bool TryReadScalarValueFromLine(
		string line,
		string elementName,
		out string value)
	{
		ArgumentNullException.ThrowIfNull(line);
		ArgumentException.ThrowIfNullOrWhiteSpace(elementName);

		if (!TryFindOpeningTagEnd(line, elementName, 0, out int contentStart))
		{
			value = string.Empty;
			return false;
		}

		string valueSlice = line[contentStart..];
		if (TryFindClosingTagStart(valueSlice, elementName, 0, out int closeTagStart))
		{
			valueSlice = valueSlice[..closeTagStart];
		}

		value = valueSlice.Trim();
		return true;
	}

	/// <summary>
	/// Attempts to read the summary start tag from a line, returning either full single-line content or
	/// the initial unclosed segment for multi-line summary accumulation.
	/// </summary>
	/// <param name="line">One line of XML-like text.</param>
	/// <param name="summaryValue">Summary content fragment from the line.</param>
	/// <param name="isClosed">Whether summary closes on the same line.</param>
	/// <returns><see langword="true"/> when summary start tag is found; otherwise <see langword="false"/>.</returns>
	private static bool TryReadSummaryStartFromLine(
		string line,
		out string summaryValue,
		out bool isClosed)
	{
		ArgumentNullException.ThrowIfNull(line);

		if (!TryFindOpeningTagEnd(line, SUMMARY_ELEMENT_NAME, 0, out int summaryStart))
		{
			summaryValue = string.Empty;
			isClosed = false;
			return false;
		}

		string summarySlice = line[summaryStart..];
		if (TryFindClosingTagStart(summarySlice, SUMMARY_ELEMENT_NAME, 0, out int summaryEnd))
		{
			summaryValue = summarySlice[..summaryEnd];
			isClosed = true;
			return true;
		}

		summaryValue = summarySlice;
		isClosed = false;
		return true;
	}

	/// <summary>
	/// Appends one summary line fragment using newline separators when needed.
	/// </summary>
	/// <param name="builder">Summary builder.</param>
	/// <param name="lineFragment">Line fragment to append.</param>
	private static void AppendSummaryLine(StringBuilder builder, string lineFragment)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(lineFragment);

		if (builder.Length > 0)
		{
			builder.Append('\n');
		}

		builder.Append(lineFragment);
	}

	/// <summary>
	/// Finds one opening tag and returns the index immediately after its closing <c>&gt;</c>.
	/// </summary>
	/// <param name="line">One line of XML-like text.</param>
	/// <param name="elementName">Element local name.</param>
	/// <param name="startIndex">Search start index.</param>
	/// <param name="contentStart">Index immediately after the opening tag.</param>
	/// <returns><see langword="true"/> when an opening tag is found; otherwise <see langword="false"/>.</returns>
	private static bool TryFindOpeningTagEnd(
		string line,
		string elementName,
		int startIndex,
		out int contentStart)
	{
		ArgumentNullException.ThrowIfNull(line);
		ArgumentException.ThrowIfNullOrWhiteSpace(elementName);

		for (int tagStart = line.IndexOf('<', startIndex); tagStart >= 0; tagStart = line.IndexOf('<', tagStart + 1))
		{
			int tagEnd = line.IndexOf('>', tagStart + 1);
			if (tagEnd < 0)
			{
				break;
			}

			string tagBody = line[(tagStart + 1)..tagEnd].Trim();
			if (tagBody.StartsWith("/", StringComparison.Ordinal))
			{
				continue;
			}

			int firstWhitespaceIndex = tagBody.IndexOfAny([' ', '\t', '\r', '\n']);
			string tagName = firstWhitespaceIndex >= 0
				? tagBody[..firstWhitespaceIndex]
				: tagBody;

			if (tagName.EndsWith("/", StringComparison.Ordinal))
			{
				tagName = tagName[..^1];
			}

			string localName = ExtractLocalName(tagName);
			if (!string.Equals(localName, elementName, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			contentStart = tagEnd + 1;
			return true;
		}

		contentStart = -1;
		return false;
	}

	/// <summary>
	/// Finds one closing tag and returns its start index.
	/// </summary>
	/// <param name="line">One line of XML-like text.</param>
	/// <param name="elementName">Element local name.</param>
	/// <param name="startIndex">Search start index.</param>
	/// <param name="closeTagStart">Closing-tag start index.</param>
	/// <returns><see langword="true"/> when a closing tag is found; otherwise <see langword="false"/>.</returns>
	private static bool TryFindClosingTagStart(
		string line,
		string elementName,
		int startIndex,
		out int closeTagStart)
	{
		ArgumentNullException.ThrowIfNull(line);
		ArgumentException.ThrowIfNullOrWhiteSpace(elementName);

		for (int tagStart = line.IndexOf("</", startIndex, StringComparison.Ordinal); tagStart >= 0; tagStart = line.IndexOf("</", tagStart + 2, StringComparison.Ordinal))
		{
			int tagEnd = line.IndexOf('>', tagStart + 2);
			if (tagEnd < 0)
			{
				continue;
			}

			string tagBody = line[(tagStart + 2)..tagEnd].Trim();
			int firstWhitespaceIndex = tagBody.IndexOfAny([' ', '\t', '\r', '\n']);
			string tagName = firstWhitespaceIndex >= 0
				? tagBody[..firstWhitespaceIndex]
				: tagBody;

			string localName = ExtractLocalName(tagName);
			if (!string.Equals(localName, elementName, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			closeTagStart = tagStart;
			return true;
		}

		closeTagStart = -1;
		return false;
	}

	/// <summary>
	/// Extracts the local name from an optional namespace-prefixed tag name.
	/// </summary>
	/// <param name="tagName">Tag name that may contain a namespace prefix.</param>
	/// <returns>Local tag name.</returns>
	private static string ExtractLocalName(string tagName)
	{
		ArgumentNullException.ThrowIfNull(tagName);

		int separatorIndex = tagName.LastIndexOf(':');
		return separatorIndex < 0
			? tagName
			: tagName[(separatorIndex + 1)..];
	}
}
