using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Contains the fully validated configuration documents produced by bootstrap.
/// </summary>
public sealed class ConfigurationDocumentSet
{
	/// <summary>
	/// Gets the validated settings document.
	/// </summary>
	public required SettingsDocument Settings
	{
		get;
		init;
	}

	/// <summary>
	/// Gets the validated manga equivalents document.
	/// </summary>
	public required MangaEquivalentsDocument MangaEquivalents
	{
		get;
		init;
	}

	/// <summary>
	/// Gets the validated scene tags document.
	/// </summary>
	public required SceneTagsDocument SceneTags
	{
		get;
		init;
	}

	/// <summary>
	/// Gets the validated source priority document.
	/// </summary>
	public required SourcePriorityDocument SourcePriority
	{
		get;
		init;
	}
}
