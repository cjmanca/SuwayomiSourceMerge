namespace SuwayomiSourceMerge.Infrastructure.Watching;

/// <summary>
/// Classifies parsed inotify event tokens.
/// </summary>
[Flags]
internal enum InotifyEventMask
{
	/// <summary>
	/// No known event tokens were parsed.
	/// </summary>
	None = 0,

	/// <summary>
	/// Inotify <c>CREATE</c> token.
	/// </summary>
	Create = 1 << 0,

	/// <summary>
	/// Inotify <c>MOVED_TO</c> token.
	/// </summary>
	MovedTo = 1 << 1,

	/// <summary>
	/// Inotify <c>CLOSE_WRITE</c> token.
	/// </summary>
	CloseWrite = 1 << 2,

	/// <summary>
	/// Inotify <c>ATTRIB</c> token.
	/// </summary>
	Attrib = 1 << 3,

	/// <summary>
	/// Inotify <c>DELETE</c> token.
	/// </summary>
	Delete = 1 << 4,

	/// <summary>
	/// Inotify <c>MOVED_FROM</c> token.
	/// </summary>
	MovedFrom = 1 << 5,

	/// <summary>
	/// Inotify <c>ISDIR</c> token.
	/// </summary>
	IsDirectory = 1 << 6,

	/// <summary>
	/// At least one unrecognized token was parsed.
	/// </summary>
	Unknown = 1 << 7
}
