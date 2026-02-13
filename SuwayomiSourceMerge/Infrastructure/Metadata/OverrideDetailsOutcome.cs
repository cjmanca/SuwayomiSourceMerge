namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Describes the terminal outcome of a details.json ensure operation.
/// </summary>
internal enum OverrideDetailsOutcome
{
	/// <summary>
	/// A details.json file already existed in an override directory, so no action was taken.
	/// </summary>
	AlreadyExists = 0,

	/// <summary>
	/// A details.json file was copied from a source directory into the preferred override directory.
	/// </summary>
	SeededFromSource = 1,

	/// <summary>
	/// A details.json file was generated from a parsed ComicInfo.xml file.
	/// </summary>
	GeneratedFromComicInfo = 2,

	/// <summary>
	/// No ComicInfo.xml candidate files were discovered.
	/// </summary>
	SkippedNoComicInfo = 3,

	/// <summary>
	/// ComicInfo.xml candidates were discovered, but none parsed successfully.
	/// </summary>
	SkippedParseFailure = 4

}

