namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Produces default values for manga equivalence mappings.
/// </summary>
/// <remarks>
/// The default document includes one starter example group so operators can copy and edit it.
/// </remarks>
public static class MangaEquivalentsDocumentDefaults
{
	/// <summary>
	/// Starter example canonical title included in generated defaults.
	/// </summary>
	private const string ExampleCanonicalTitle = "Example Manga Title";

	/// <summary>
	/// Starter example alias included in generated defaults.
	/// </summary>
	private const string ExampleAliasTitle = "Example Manga Alt Title";

	/// <summary>
	/// Creates a default equivalence document.
	/// </summary>
	/// <returns>
	/// A document with an initialized <see cref="MangaEquivalentsDocument.Groups"/> list containing one example.
	/// </returns>
	public static MangaEquivalentsDocument Create()
	{
		return new MangaEquivalentsDocument
		{
			Groups =
			[
				new MangaEquivalentGroup
				{
					Canonical = ExampleCanonicalTitle,
					Aliases =
					[
						ExampleAliasTitle
					]
				}
			]
		};
	}
}
