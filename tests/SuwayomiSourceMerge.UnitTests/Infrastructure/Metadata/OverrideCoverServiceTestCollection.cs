namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

/// <summary>
/// Defines one non-parallel xUnit collection for tests that observe process-wide OverrideCoverService lock state.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OverrideCoverServiceTestCollection
{
	/// <summary>
	/// xUnit collection name for sequential OverrideCoverService tests.
	/// </summary>
	public const string Name = "OverrideCoverService Sequential";
}
