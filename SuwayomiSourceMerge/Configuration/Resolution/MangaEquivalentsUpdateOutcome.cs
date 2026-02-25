namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Specifies deterministic outcomes for manga-equivalents update operations.
/// </summary>
internal enum MangaEquivalentsUpdateOutcome
{
	/// <summary>
	/// The request matched exactly one group and appended one or more missing aliases.
	/// </summary>
	UpdatedExistingGroup,

	/// <summary>
	/// The request matched no groups and created one new canonical group.
	/// </summary>
	CreatedNewGroup,

	/// <summary>
	/// The request produced no document mutations.
	/// </summary>
	NoChanges,

	/// <summary>
	/// The request matched multiple existing groups and was rejected.
	/// </summary>
	Conflict,

	/// <summary>
	/// Parsing or validation failed for one or more involved configuration documents.
	/// </summary>
	ValidationFailed,

	/// <summary>
	/// Source document files could not be read.
	/// </summary>
	ReadFailed,

	/// <summary>
	/// Persisting the updated document failed.
	/// </summary>
	WriteFailed
}
