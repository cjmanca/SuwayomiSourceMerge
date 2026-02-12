namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Produces default source priority configuration values.
/// </summary>
/// <remarks>
/// The default intentionally contains no explicit source ordering so operators can opt into precedence
/// only when needed.
/// </remarks>
public static class SourcePriorityDocumentDefaults
{
	/// <summary>
	/// Creates an empty source priority list.
	/// </summary>
	/// <returns>A document with an initialized, empty <see cref="SourcePriorityDocument.Sources"/> list.</returns>
	public static SourcePriorityDocument Create()
	{
		return new SourcePriorityDocument
		{
			Sources = []
		};
	}
}
