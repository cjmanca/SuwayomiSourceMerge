using System.Text;

namespace SuwayomiSourceMerge.Infrastructure.Processes;

/// <summary>
/// Stores bounded text output for a single command stream and tracks truncation state.
/// </summary>
internal sealed class BoundedOutputBuffer
{
	/// <summary>
	/// Synchronization gate for concurrent append/snapshot operations.
	/// </summary>
	private readonly Lock _syncRoot = new();

	/// <summary>
	/// Backing builder for captured output text.
	/// </summary>
	private readonly StringBuilder _builder;

	/// <summary>
	/// Maximum number of characters retained by this buffer.
	/// </summary>
	private readonly int _maxCharacters;

	/// <summary>
	/// Indicates whether incoming output exceeded the configured maximum.
	/// </summary>
	private bool _isTruncated;

	/// <summary>
	/// Initializes a new instance of the <see cref="BoundedOutputBuffer"/> class.
	/// </summary>
	/// <param name="maxCharacters">Maximum number of characters to retain.</param>
	public BoundedOutputBuffer(int maxCharacters)
	{
		if (maxCharacters <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxCharacters), "Maximum characters must be greater than zero.");
		}

		_maxCharacters = maxCharacters;
		_builder = new StringBuilder(Math.Min(maxCharacters, 4096));
	}

	/// <summary>
	/// Appends output characters while enforcing the configured maximum size.
	/// </summary>
	/// <param name="characters">Source character buffer.</param>
	/// <param name="count">Number of characters to append.</param>
	public void Append(char[] characters, int count)
	{
		ArgumentNullException.ThrowIfNull(characters);

		if (count <= 0)
		{
			return;
		}

		lock (_syncRoot)
		{
			if (_isTruncated)
			{
				return;
			}

			int remaining = _maxCharacters - _builder.Length;
			if (remaining <= 0)
			{
				_isTruncated = true;
				return;
			}

			int appendLength = Math.Min(remaining, count);
			_builder.Append(characters, 0, appendLength);

			if (appendLength < count)
			{
				_isTruncated = true;
			}
		}
	}

	/// <summary>
	/// Returns a thread-safe snapshot of captured output and truncation state.
	/// </summary>
	/// <returns>Tuple containing captured text and truncation state.</returns>
	public (string Text, bool IsTruncated) GetSnapshot()
	{
		lock (_syncRoot)
		{
			return (_builder.ToString(), _isTruncated);
		}
	}
}
