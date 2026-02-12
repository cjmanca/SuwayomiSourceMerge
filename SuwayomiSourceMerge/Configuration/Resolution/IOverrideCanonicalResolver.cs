namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Resolves existing override directory titles to preserve exact title spelling when possible.
/// </summary>
internal interface IOverrideCanonicalResolver
{
	/// <summary>
	/// Attempts to resolve an override canonical title for the provided input title.
	/// </summary>
	/// <param name="inputTitle">Input title to resolve.</param>
	/// <param name="overrideCanonicalTitle">
	/// Exact existing override title when found; otherwise an empty string.
	/// </param>
	/// <returns>
	/// <see langword="true"/> when a matching override canonical title exists; otherwise <see langword="false"/>.
	/// </returns>
	bool TryResolveOverrideCanonical(string inputTitle, out string overrideCanonicalTitle);
}
