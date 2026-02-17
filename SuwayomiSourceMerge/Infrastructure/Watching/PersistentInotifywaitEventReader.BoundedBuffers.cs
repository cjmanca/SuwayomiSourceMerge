namespace SuwayomiSourceMerge.Infrastructure.Watching;

internal sealed partial class PersistentInotifywaitEventReader
{
	/// <summary>
	/// Maximum number of events retained in one poll result.
	/// </summary>
	private const int MaxPollEvents = 4096;

	/// <summary>
	/// Maximum number of warnings retained in one poll result.
	/// </summary>
	private const int MaxPollWarnings = 1024;

	/// <summary>
	/// Appends one bounded-overflow summary warning when poll buffers dropped items.
	/// </summary>
	/// <param name="warnings">Warning buffer.</param>
	/// <param name="droppedEventCount">Dropped event count.</param>
	/// <param name="droppedWarningCount">Dropped warning count.</param>
	private static void AddPollOverflowWarning(
		BoundedFifoBuffer<string> warnings,
		int droppedEventCount,
		int droppedWarningCount)
	{
		if (droppedEventCount <= 0 && droppedWarningCount <= 0)
		{
			return;
		}

		warnings.AddOverflowSummary(
			$"Persistent inotify poll buffers overflowed. dropped_events='{droppedEventCount}' dropped_warnings='{droppedWarningCount}' max_events='{MaxPollEvents}' max_warnings='{MaxPollWarnings}' policy='drop_oldest'.");
	}

	/// <summary>
	/// Fixed-capacity FIFO buffer that drops oldest items on overflow.
	/// </summary>
	/// <typeparam name="T">Buffer item type.</typeparam>
	private sealed class BoundedFifoBuffer<T>
	{
		private readonly int _maxItems;
		private readonly Queue<T> _items = new();
		private int _droppedCount;

		public BoundedFifoBuffer(int maxItems)
		{
			if (maxItems <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxItems), "Maximum items must be > 0.");
			}

			_maxItems = maxItems;
		}

		public int DroppedCount => _droppedCount;
		public int Count => _items.Count;

		public void Add(T item)
		{
			if (_items.Count >= _maxItems)
			{
				_ = _items.Dequeue();
				_droppedCount++;
			}

			_items.Enqueue(item);
		}

		public void AddOverflowSummary(T item)
		{
			if (_items.Count >= _maxItems)
			{
				_ = _items.Dequeue();
			}

			_items.Enqueue(item);
		}

		public void Clear()
		{
			_items.Clear();
			_droppedCount = 0;
		}

		public T[] ToArray()
		{
			return _items.ToArray();
		}
	}
}
