namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Produces default source priority configuration values.
/// </summary>
/// <remarks>
/// The default includes one starter example entry so operators can copy and edit it.
/// </remarks>
public static class SourcePriorityDocumentDefaults
{
	/// <summary>
	/// Starter example source name included in generated defaults.
	/// </summary>
	private const string ExampleSourceName = "Example Source Name";

	/// <summary>
	/// Creates a source priority list with one starter example entry.
	/// </summary>
	/// <returns>
	/// A document with an initialized <see cref="SourcePriorityDocument.Sources"/> list containing one example.
	/// </returns>
	public static SourcePriorityDocument Create()
	{
		return new SourcePriorityDocument
		{
			Sources =
			[
				ExampleSourceName
			]
		};
	}
}
