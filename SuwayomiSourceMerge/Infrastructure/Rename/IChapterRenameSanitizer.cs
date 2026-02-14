namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Defines chapter-directory rename sanitization behavior.
/// </summary>
internal interface IChapterRenameSanitizer
{
	/// <summary>
	/// Produces a sanitized chapter directory name.
	/// </summary>
	/// <param name="name">Original chapter directory name.</param>
	/// <returns>Sanitized chapter directory name. Returns the original name when no rewrite is required.</returns>
	string Sanitize(string name);
}

