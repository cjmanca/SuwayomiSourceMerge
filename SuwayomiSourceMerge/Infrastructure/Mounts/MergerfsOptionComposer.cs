namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Composes mergerfs mount options with required runtime safety defaults.
/// </summary>
internal static class MergerfsOptionComposer
{
	/// <summary>
	/// Default mergerfs thread cap applied when no explicit threads option exists.
	/// </summary>
	public const int DefaultThreadCount = 1;

	/// <summary>
	/// Composes mount options from settings base options and one mount identity token.
	/// </summary>
	/// <param name="mergerfsOptionsBase">Configured base options string.</param>
	/// <param name="desiredIdentity">Desired mount identity token.</param>
	/// <returns>Composed mergerfs options string.</returns>
	public static string ComposeMountOptions(string mergerfsOptionsBase, string desiredIdentity)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mergerfsOptionsBase);
		ArgumentException.ThrowIfNullOrWhiteSpace(desiredIdentity);

		string normalizedBase = mergerfsOptionsBase.Trim().TrimEnd(',');
		if (string.IsNullOrWhiteSpace(normalizedBase))
		{
			normalizedBase = $"threads={DefaultThreadCount}";
		}

		if (!HasThreadsOption(normalizedBase))
		{
			normalizedBase = string.Create(
				System.Globalization.CultureInfo.InvariantCulture,
				$"{normalizedBase},threads={DefaultThreadCount}");
		}

		return string.Create(
			System.Globalization.CultureInfo.InvariantCulture,
			$"{normalizedBase},fsname={desiredIdentity}");
	}

	/// <summary>
	/// Returns whether an option list already contains one threads option token.
	/// </summary>
	/// <param name="options">Comma-separated options list.</param>
	/// <returns><see langword="true"/> when a threads option token is present; otherwise <see langword="false"/>.</returns>
	private static bool HasThreadsOption(string options)
	{
		string[] tokens = options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		for (int index = 0; index < tokens.Length; index++)
		{
			string token = tokens[index];
			if (token.Equals("threads", StringComparison.OrdinalIgnoreCase) ||
				token.StartsWith("threads=", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}
}
