namespace SuwayomiSourceMerge.UnitTests;

using SuwayomiSourceMerge.Testing;

/// <summary>
/// Smoke tests that confirm the test framework and basic test wiring.
/// </summary>
public class ProjectLayoutTests
{
    /// <summary>
    /// Verifies shared test utility access from unit tests.
    /// </summary>
    [Fact]
    public void Combine_ShouldJoinSegments()
    {
        string combined = TestPathHelper.Combine("tests", "sample");

        Assert.EndsWith("tests" + Path.DirectorySeparatorChar + "sample", combined);
    }
}
