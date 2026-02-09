namespace SuwayomiSourceMerge.Testing;

/// <summary>
/// Provides small shared helpers for tests.
/// </summary>
public static class TestPathHelper
{
    /// <summary>
    /// Combines path segments using the current platform separator.
    /// </summary>
    /// <param name="segments">The path segments to combine.</param>
    /// <returns>A combined path.</returns>
    public static string Combine(params string[] segments)
    {
        return Path.Combine(segments);
    }
}
