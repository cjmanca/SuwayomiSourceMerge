namespace SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Provides reusable path-focused test helpers for cross-platform behavior checks.
/// </summary>
internal static class PathTestUtilities
{
	/// <summary>
	/// Produces a case-variant representation of the provided path string.
	/// </summary>
	/// <param name="path">Path to mutate.</param>
	/// <returns>Case-variant path string.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
	public static string InvertPathCase(string path)
	{
		ArgumentNullException.ThrowIfNull(path);

		char[] characters = path.ToCharArray();
		for (int index = 0; index < characters.Length; index++)
		{
			char character = characters[index];
			if (!char.IsLetter(character))
			{
				continue;
			}

			characters[index] = char.IsUpper(character)
				? char.ToLowerInvariant(character)
				: char.ToUpperInvariant(character);
		}

		return new string(characters);
	}
}
