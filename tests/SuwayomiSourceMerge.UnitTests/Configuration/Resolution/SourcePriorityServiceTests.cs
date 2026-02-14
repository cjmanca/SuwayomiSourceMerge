namespace SuwayomiSourceMerge.UnitTests.Configuration.Resolution;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Tests source-priority resolution from YAML-backed source-priority mappings.
/// </summary>
public sealed class SourcePriorityServiceTests
{
	[Fact]
	public void TryGetPriority_ShouldReturnConfiguredPriority_WhenSourceIsConfigured()
	{
		SourcePriorityService service = new(CreateDocument());

		bool wasResolved = service.TryGetPriority("Source B", out int priority);

		Assert.True(wasResolved);
		Assert.Equal(1, priority);
	}

	[Fact]
	public void TryGetPriority_ShouldResolveNormalizedVariant_WhenConfiguredSourceExists()
	{
		SourcePriorityService service = new(CreateDocument());

		bool wasResolved = service.TryGetPriority("source-a", out int priority);

		Assert.True(wasResolved);
		Assert.Equal(0, priority);
	}

	[Fact]
	public void TryGetPriority_ShouldRemainStable_ForRepeatedNormalizedLookups()
	{
		SourcePriorityService service = new(CreateDocument());

		bool firstResolved = service.TryGetPriority("source-a", out int firstPriority);
		bool secondResolved = service.TryGetPriority("source-a", out int secondPriority);

		Assert.True(firstResolved);
		Assert.True(secondResolved);
		Assert.Equal(firstPriority, secondPriority);
		Assert.Equal(0, firstPriority);
	}

	[Fact]
	public void GetPriorityOrDefault_ShouldReturnFallback_WhenSourceIsNotConfigured()
	{
		SourcePriorityService service = new(CreateDocument());

		int priority = service.GetPriorityOrDefault("Unknown Source", unknownPriority: 999);

		Assert.Equal(999, priority);
	}

	[Fact]
	public void TryGetPriority_ShouldReturnFalseAndMaxValue_WhenNonEmptySourceIsNotConfigured()
	{
		SourcePriorityService service = new(CreateDocument());

		bool wasResolved = service.TryGetPriority("Unknown Source", out int priority);

		Assert.False(wasResolved);
		Assert.Equal(int.MaxValue, priority);
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenNormalizedSourcesDuplicate()
	{
		SourcePriorityDocument document = new()
		{
			Sources = ["Source A", "source-a"]
		};

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new SourcePriorityService(document));
		Assert.Contains("duplicates an existing normalized source key", exception.Message);
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenSourcesListIsMissing()
	{
		SourcePriorityDocument document = new()
		{
			Sources = null
		};

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new SourcePriorityService(document));
		Assert.Contains("sources list", exception.Message);
	}

	[Fact]
	public void TryGetPriority_ShouldReturnFalse_WhenSourceNormalizesToEmpty()
	{
		SourcePriorityService service = new(CreateDocument());

		bool wasResolved = service.TryGetPriority("!!!", out int priority);

		Assert.False(wasResolved);
		Assert.Equal(int.MaxValue, priority);
	}

	[Fact]
	public void TryGetPriority_ShouldThrow_WhenSourceNameIsNull()
	{
		SourcePriorityService service = new(CreateDocument());

		Assert.Throws<ArgumentNullException>(() => service.TryGetPriority(null!, out _));
	}

	[Fact]
	public void GetPriorityOrDefault_ShouldThrow_WhenSourceNameIsNull()
	{
		SourcePriorityService service = new(CreateDocument());

		Assert.Throws<ArgumentNullException>(() => service.GetPriorityOrDefault(null!));
	}

	private static SourcePriorityDocument CreateDocument()
	{
		return new SourcePriorityDocument
		{
			Sources = ["Source A", "Source B", "Source C"]
		};
	}
}
