using System.Text;

namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Parses <c>findmnt -P</c> output lines into <see cref="MountSnapshotEntry"/> values.
/// </summary>
internal static class FindmntSnapshotLineParser
{
	/// <summary>
	/// Attempts to parse one <c>findmnt -P</c> output line into a mount entry.
	/// </summary>
	/// <param name="line">Raw output line.</param>
	/// <param name="entry">Parsed mount entry on success.</param>
	/// <param name="warningMessage">Parse warning details on failure.</param>
	/// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
	public static bool TryParse(
		string line,
		out MountSnapshotEntry? entry,
		out string? warningMessage)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(line);

		entry = null;
		warningMessage = null;

		if (!TryParseKeyValuePairs(line, out IReadOnlyDictionary<string, string>? pairs, out warningMessage))
		{
			return false;
		}

		IReadOnlyDictionary<string, string> parsedPairs = pairs!;

		if (!parsedPairs.TryGetValue("TARGET", out string? targetRaw) || string.IsNullOrWhiteSpace(targetRaw))
		{
			warningMessage = "missing TARGET field";
			return false;
		}

		if (!parsedPairs.TryGetValue("FSTYPE", out string? fileSystemTypeRaw) || string.IsNullOrWhiteSpace(fileSystemTypeRaw))
		{
			warningMessage = "missing FSTYPE field";
			return false;
		}

		string sourceRaw = parsedPairs.TryGetValue("SOURCE", out string? foundSourceRaw)
			? foundSourceRaw
			: string.Empty;
		string optionsRaw = parsedPairs.TryGetValue("OPTIONS", out string? foundOptionsRaw)
			? foundOptionsRaw
			: string.Empty;

		string target = DecodeEscapedValue(targetRaw);
		string fileSystemType = DecodeEscapedValue(fileSystemTypeRaw);
		string source = DecodeEscapedValue(sourceRaw);
		string options = DecodeEscapedValue(optionsRaw);

		if (string.IsNullOrWhiteSpace(target))
		{
			warningMessage = "decoded TARGET field is empty";
			return false;
		}

		if (string.IsNullOrWhiteSpace(fileSystemType))
		{
			warningMessage = "decoded FSTYPE field is empty";
			return false;
		}

		entry = new MountSnapshotEntry(
			target,
			fileSystemType,
			source,
			options,
			isHealthy: null);
		return true;
	}

	/// <summary>
	/// Parses quoted <c>KEY="value"</c> pairs from one line of <c>findmnt -P</c> output.
	/// </summary>
	/// <param name="line">Raw line to parse.</param>
	/// <param name="pairs">Parsed key/value collection on success.</param>
	/// <param name="warningMessage">Parse warning details on failure.</param>
	/// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryParseKeyValuePairs(
		string line,
		out IReadOnlyDictionary<string, string>? pairs,
		out string? warningMessage)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(line);

		Dictionary<string, string> parsedPairs = new(StringComparer.OrdinalIgnoreCase);
		int index = 0;

		while (index < line.Length)
		{
			while (index < line.Length && char.IsWhiteSpace(line[index]))
			{
				index++;
			}

			if (index >= line.Length)
			{
				break;
			}

			int keyStart = index;
			while (index < line.Length && line[index] != '=' && !char.IsWhiteSpace(line[index]))
			{
				index++;
			}

			if (index <= keyStart || index >= line.Length || line[index] != '=')
			{
				pairs = null;
				warningMessage = "failed to read key token";
				return false;
			}

			string key = line[keyStart..index];
			index++;

			if (index >= line.Length || line[index] != '"')
			{
				pairs = null;
				warningMessage = $"key '{key}' is not followed by a quoted value";
				return false;
			}

			index++;
			StringBuilder valueBuilder = new();
			bool terminated = false;
			while (index < line.Length)
			{
				char character = line[index];
				if (character == '"' && IsUnescapedQuote(line, index))
				{
					terminated = true;
					index++;
					break;
				}

				valueBuilder.Append(character);
				index++;
			}

			if (!terminated)
			{
				pairs = null;
				warningMessage = $"unterminated quoted value for key '{key}'";
				return false;
			}

			parsedPairs[key] = valueBuilder.ToString();

			if (index < line.Length && !char.IsWhiteSpace(line[index]))
			{
				pairs = null;
				warningMessage = $"unexpected character '{line[index]}' after key '{key}'";
				return false;
			}
		}

		pairs = parsedPairs;
		warningMessage = null;
		return true;
	}

	/// <summary>
	/// Determines whether a quote at the provided index is unescaped by counting trailing backslashes.
	/// </summary>
	/// <param name="value">Source value containing the candidate quote.</param>
	/// <param name="quoteIndex">Index of the candidate quote.</param>
	/// <returns><see langword="true"/> when the quote is not escaped; otherwise <see langword="false"/>.</returns>
	private static bool IsUnescapedQuote(string value, int quoteIndex)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentOutOfRangeException.ThrowIfNegative(quoteIndex);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(quoteIndex, value.Length);

		int trailingBackslashCount = 0;
		for (int index = quoteIndex - 1; index >= 0 && value[index] == '\\'; index--)
		{
			trailingBackslashCount++;
		}

		return (trailingBackslashCount % 2) == 0;
	}

	/// <summary>
	/// Decodes escaped values emitted by <c>findmnt</c>.
	/// </summary>
	/// <param name="rawValue">Raw escaped value.</param>
	/// <returns>Decoded value text.</returns>
	private static string DecodeEscapedValue(string rawValue)
	{
		ArgumentNullException.ThrowIfNull(rawValue);

		StringBuilder decoded = new(rawValue.Length);
		int index = 0;

		while (index < rawValue.Length)
		{
			char current = rawValue[index];
			if (current != '\\')
			{
				decoded.Append(current);
				index++;
				continue;
			}

			if (index == rawValue.Length - 1)
			{
				decoded.Append('\\');
				index++;
				continue;
			}

			char next = rawValue[index + 1];
			if (TryDecodeOctal(rawValue, index, out char decodedOctalCharacter, out int octalLength))
			{
				decoded.Append(decodedOctalCharacter);
				index += octalLength;
				continue;
			}

			if (TryDecodeHex(rawValue, index, out char decodedHexCharacter, out int hexLength))
			{
				decoded.Append(decodedHexCharacter);
				index += hexLength;
				continue;
			}

			switch (next)
			{
				case 'n':
					decoded.Append('\n');
					break;
				case 'r':
					decoded.Append('\r');
					break;
				case 't':
					decoded.Append('\t');
					break;
				case '"':
					decoded.Append('"');
					break;
				case '\\':
					decoded.Append('\\');
					break;
				default:
					decoded.Append(next);
					break;
			}

			index += 2;
		}

		return decoded.ToString();
	}

	/// <summary>
	/// Attempts to decode an octal escape sequence beginning at a backslash position.
	/// </summary>
	/// <param name="value">Source text.</param>
	/// <param name="slashIndex">Index of the backslash character.</param>
	/// <param name="decodedCharacter">Decoded character on success.</param>
	/// <param name="consumedLength">Number of consumed source characters.</param>
	/// <returns><see langword="true"/> when decoding succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryDecodeOctal(
		string value,
		int slashIndex,
		out char decodedCharacter,
		out int consumedLength)
	{
		ArgumentNullException.ThrowIfNull(value);

		decodedCharacter = default;
		consumedLength = 0;

		int startIndex = slashIndex + 1;
		if (startIndex >= value.Length || value[startIndex] is < '0' or > '7')
		{
			return false;
		}

		int maxIndex = Math.Min(value.Length, startIndex + 3);
		int parsedLength = 0;
		int octalValue = 0;
		for (int index = startIndex; index < maxIndex; index++)
		{
			char current = value[index];
			if (current is < '0' or > '7')
			{
				break;
			}

			octalValue = (octalValue * 8) + (current - '0');
			parsedLength++;
		}

		if (parsedLength == 0)
		{
			return false;
		}

		decodedCharacter = (char)octalValue;
		consumedLength = 1 + parsedLength;
		return true;
	}

	/// <summary>
	/// Attempts to decode a hexadecimal escape sequence beginning at a backslash position.
	/// </summary>
	/// <param name="value">Source text.</param>
	/// <param name="slashIndex">Index of the backslash character.</param>
	/// <param name="decodedCharacter">Decoded character on success.</param>
	/// <param name="consumedLength">Number of consumed source characters.</param>
	/// <returns><see langword="true"/> when decoding succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryDecodeHex(
		string value,
		int slashIndex,
		out char decodedCharacter,
		out int consumedLength)
	{
		ArgumentNullException.ThrowIfNull(value);

		decodedCharacter = default;
		consumedLength = 0;

		int prefixIndex = slashIndex + 1;
		if (prefixIndex >= value.Length || (value[prefixIndex] != 'x' && value[prefixIndex] != 'X'))
		{
			return false;
		}

		int startIndex = prefixIndex + 1;
		if (startIndex >= value.Length || !IsHex(value[startIndex]))
		{
			return false;
		}

		int endIndex = startIndex;
		int maxIndex = Math.Min(value.Length, startIndex + 2);
		while (endIndex < maxIndex && IsHex(value[endIndex]))
		{
			endIndex++;
		}

		if (!int.TryParse(value[startIndex..endIndex], System.Globalization.NumberStyles.HexNumber, null, out int hexValue))
		{
			return false;
		}

		decodedCharacter = (char)hexValue;
		consumedLength = endIndex - slashIndex;
		return true;
	}

	/// <summary>
	/// Determines whether a character is a hexadecimal digit.
	/// </summary>
	/// <param name="character">Character to inspect.</param>
	/// <returns><see langword="true"/> when hexadecimal; otherwise <see langword="false"/>.</returns>
	private static bool IsHex(char character)
	{
		return character is >= '0' and <= '9'
			|| character is >= 'a' and <= 'f'
			|| character is >= 'A' and <= 'F';
	}
}
