namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Classifies deterministic outcomes for runtime manga-equivalence catalog update operations.
/// </summary>
internal enum MangaEquivalenceCatalogUpdateOutcome
{
	/// <summary>
	/// Updater persistence succeeded and runtime resolver snapshot was reloaded and swapped.
	/// </summary>
	Applied,

	/// <summary>
	/// Updater reported no document changes, so runtime snapshot remained unchanged.
	/// </summary>
	NoChanges,

	/// <summary>
	/// Updater failed before or during persistence, so runtime snapshot remained unchanged.
	/// </summary>
	UpdateFailed,

	/// <summary>
	/// Updater persistence succeeded, but reloading persisted state failed, so runtime snapshot remained unchanged.
	/// </summary>
	ReloadFailed
}
