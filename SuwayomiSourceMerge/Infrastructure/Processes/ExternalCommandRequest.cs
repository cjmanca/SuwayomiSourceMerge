namespace SuwayomiSourceMerge.Infrastructure.Processes;

/// <summary>
/// Represents the inputs and limits used for external command execution.
/// </summary>
internal sealed class ExternalCommandRequest
{
	/// <summary>
	/// Default timeout used when no explicit timeout is provided.
	/// </summary>
	private static readonly TimeSpan DEFAULT_TIMEOUT = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Default polling interval used while supervising process completion.
	/// </summary>
	private static readonly TimeSpan DEFAULT_POLL_INTERVAL = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Default maximum number of characters captured per output stream.
	/// </summary>
	private const int DEFAULT_MAX_OUTPUT_CHARACTERS = 65536;

	/// <summary>
	/// Gets or sets the executable file name or path.
	/// </summary>
	public string FileName
	{
		get;
		init;
	} = string.Empty;

	/// <summary>
	/// Gets or sets command-line arguments passed to the executable.
	/// </summary>
	public IReadOnlyList<string> Arguments
	{
		get;
		init;
	} = [];

	/// <summary>
	/// Gets or sets the maximum duration allowed for command execution.
	/// </summary>
	public TimeSpan Timeout
	{
		get;
		init;
	} = DEFAULT_TIMEOUT;

	/// <summary>
	/// Gets or sets the polling interval used while waiting for command completion.
	/// </summary>
	public TimeSpan PollInterval
	{
		get;
		init;
	} = DEFAULT_POLL_INTERVAL;

	/// <summary>
	/// Gets or sets the maximum number of characters captured for each output stream.
	/// </summary>
	public int MaxOutputCharacters
	{
		get;
		init;
	} = DEFAULT_MAX_OUTPUT_CHARACTERS;
}
