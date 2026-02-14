namespace SuwayomiSourceMerge.Infrastructure.Watching;

/// <summary>
/// Represents one parsed inotify event output line.
/// </summary>
internal sealed class InotifyEventRecord
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InotifyEventRecord"/> class.
	/// </summary>
	/// <param name="path">Event path parsed from <c>%w%f</c>.</param>
	/// <param name="eventMask">Bitmask parsed from event-token text.</param>
	/// <param name="rawEvents">Raw event-token text parsed from <c>%e</c>.</param>
	public InotifyEventRecord(string path, InotifyEventMask eventMask, string rawEvents)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentNullException.ThrowIfNull(rawEvents);

		Path = path;
		EventMask = eventMask;
		RawEvents = rawEvents;
	}

	/// <summary>
	/// Gets the filesystem path reported by inotify.
	/// </summary>
	public string Path
	{
		get;
	}

	/// <summary>
	/// Gets parsed event token flags.
	/// </summary>
	public InotifyEventMask EventMask
	{
		get;
	}

	/// <summary>
	/// Gets raw comma-separated token text reported by inotify.
	/// </summary>
	public string RawEvents
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether <see cref="EventMask"/> contains <see cref="InotifyEventMask.IsDirectory"/>.
	/// </summary>
	public bool IsDirectory
	{
		get
		{
			return EventMask.HasFlag(InotifyEventMask.IsDirectory);
		}
	}

	/// <summary>
	/// Attempts to parse one formatted inotify line.
	/// </summary>
	/// <param name="line">Input line formatted as <c>%w%f|%e</c>.</param>
	/// <param name="record">Parsed record when parsing succeeds.</param>
	/// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
	public static bool TryParse(string line, out InotifyEventRecord? record)
	{
		record = null;

		if (string.IsNullOrWhiteSpace(line))
		{
			return false;
		}

		int separatorIndex = line.LastIndexOf("|", StringComparison.Ordinal);
		if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
		{
			return false;
		}

		string path = line[..separatorIndex].Trim();
		string rawEvents = line[(separatorIndex + 1)..].Trim();
		if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(rawEvents))
		{
			return false;
		}

		InotifyEventMask mask = ParseEventMask(rawEvents);
		record = new InotifyEventRecord(path, mask, rawEvents);
		return true;
	}

	/// <summary>
	/// Parses one comma-separated event-token list into bit flags.
	/// </summary>
	/// <param name="rawEvents">Raw event-token text.</param>
	/// <returns>Parsed event-token mask.</returns>
	private static InotifyEventMask ParseEventMask(string rawEvents)
	{
		InotifyEventMask mask = InotifyEventMask.None;
		string[] tokens = rawEvents.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		for (int index = 0; index < tokens.Length; index++)
		{
			string token = tokens[index];
			if (token.Equals("CREATE", StringComparison.OrdinalIgnoreCase))
			{
				mask |= InotifyEventMask.Create;
				continue;
			}

			if (token.Equals("MOVED_TO", StringComparison.OrdinalIgnoreCase))
			{
				mask |= InotifyEventMask.MovedTo;
				continue;
			}

			if (token.Equals("CLOSE_WRITE", StringComparison.OrdinalIgnoreCase))
			{
				mask |= InotifyEventMask.CloseWrite;
				continue;
			}

			if (token.Equals("ATTRIB", StringComparison.OrdinalIgnoreCase))
			{
				mask |= InotifyEventMask.Attrib;
				continue;
			}

			if (token.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
			{
				mask |= InotifyEventMask.Delete;
				continue;
			}

			if (token.Equals("MOVED_FROM", StringComparison.OrdinalIgnoreCase))
			{
				mask |= InotifyEventMask.MovedFrom;
				continue;
			}

			if (token.Equals("ISDIR", StringComparison.OrdinalIgnoreCase))
			{
				mask |= InotifyEventMask.IsDirectory;
				continue;
			}

			mask |= InotifyEventMask.Unknown;
		}

		return mask;
	}
}
