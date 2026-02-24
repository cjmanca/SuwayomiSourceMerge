namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// details.json model helpers for <see cref="OverrideDetailsService"/>.
/// </summary>
internal sealed partial class OverrideDetailsService
{
	/// <summary>
	/// Immutable details.json content model used by writer helpers.
	/// </summary>
	private sealed class DetailsJsonDocumentModel
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DetailsJsonDocumentModel"/> class.
		/// </summary>
		/// <param name="title">Title value.</param>
		/// <param name="author">Author value.</param>
		/// <param name="artist">Artist value.</param>
		/// <param name="description">Description value.</param>
		/// <param name="genres">Genre array values.</param>
		/// <param name="status">Status value.</param>
		public DetailsJsonDocumentModel(
			string title,
			string author,
			string artist,
			string description,
			IReadOnlyList<string> genres,
			string status)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(title);
			ArgumentNullException.ThrowIfNull(author);
			ArgumentNullException.ThrowIfNull(artist);
			ArgumentNullException.ThrowIfNull(description);
			ArgumentNullException.ThrowIfNull(genres);
			ArgumentException.ThrowIfNullOrWhiteSpace(status);

			Title = title.Trim();
			Author = author.Trim();
			Artist = artist.Trim();
			Description = description;
			Genres = genres
				.Where(static genre => !string.IsNullOrWhiteSpace(genre))
				.Select(static genre => genre.Trim())
				.ToArray();
			Status = status.Trim();
		}

		/// <summary>
		/// Gets title field value.
		/// </summary>
		public string Title
		{
			get;
		}

		/// <summary>
		/// Gets author field value.
		/// </summary>
		public string Author
		{
			get;
		}

		/// <summary>
		/// Gets artist field value.
		/// </summary>
		public string Artist
		{
			get;
		}

		/// <summary>
		/// Gets description field value.
		/// </summary>
		public string Description
		{
			get;
		}

		/// <summary>
		/// Gets genre array values.
		/// </summary>
		public IReadOnlyList<string> Genres
		{
			get;
		}

		/// <summary>
		/// Gets status field value.
		/// </summary>
		public string Status
		{
			get;
		}
	}
}
