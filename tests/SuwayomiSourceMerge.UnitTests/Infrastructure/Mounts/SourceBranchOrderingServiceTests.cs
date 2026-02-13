namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies source branch ordering and de-duplication behavior.
/// </summary>
public sealed class SourceBranchOrderingServiceTests
{
	/// <summary>
	/// Verifies source branches are ordered by configured priority, then source name, then source path.
	/// </summary>
	[Fact]
	public void Order_Expected_ShouldSortByPriorityThenSourceNameThenPath()
	{
		SourceBranchOrderingService service = new();
		FakeSourcePriorityService priorityService = new(
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				["Source B"] = 1,
				["Source A"] = 0
			});

		IReadOnlyList<MergerfsSourceBranchCandidate> ordered = service.Order(
			[
				new MergerfsSourceBranchCandidate("Source B", "/ssm/sources/disk2/source-b"),
				new MergerfsSourceBranchCandidate("Source A", "/ssm/sources/disk1/source-a"),
				new MergerfsSourceBranchCandidate("Source C", "/ssm/sources/disk3/source-c")
			],
			priorityService);

		Assert.Equal(
			["Source A", "Source B", "Source C"],
			ordered.Select(candidate => candidate.SourceName).ToArray());
	}

	/// <summary>
	/// Verifies duplicate source paths are removed after deterministic ordering.
	/// </summary>
	[Fact]
	public void Order_Edge_ShouldDeduplicateBySourcePath_AfterSorting()
	{
		SourceBranchOrderingService service = new();
		FakeSourcePriorityService priorityService = new(
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				["Preferred"] = 0,
				["Secondary"] = 1
			});
		string sharedSourcePath = "/ssm/sources/disk1/shared-path";

		IReadOnlyList<MergerfsSourceBranchCandidate> ordered = service.Order(
			[
				new MergerfsSourceBranchCandidate("Secondary", sharedSourcePath),
				new MergerfsSourceBranchCandidate("Preferred", sharedSourcePath)
			],
			priorityService);

		MergerfsSourceBranchCandidate selected = Assert.Single(ordered);
		Assert.Equal("Preferred", selected.SourceName);
		Assert.Equal(Path.GetFullPath(sharedSourcePath), selected.SourcePath);
	}

	/// <summary>
	/// Verifies unknown sources retain deterministic ordering with fallback priority.
	/// </summary>
	[Fact]
	public void Order_Edge_ShouldUseFallbackPriorityForUnknownSources()
	{
		SourceBranchOrderingService service = new();
		FakeSourcePriorityService priorityService = new(
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				["Known"] = 0
			});

		IReadOnlyList<MergerfsSourceBranchCandidate> ordered = service.Order(
			[
				new MergerfsSourceBranchCandidate("Unknown Z", "/ssm/sources/z"),
				new MergerfsSourceBranchCandidate("Known", "/ssm/sources/a"),
				new MergerfsSourceBranchCandidate("Unknown A", "/ssm/sources/y")
			],
			priorityService);

		Assert.Equal(
			["Known", "Unknown A", "Unknown Z"],
			ordered.Select(candidate => candidate.SourceName).ToArray());
	}

	/// <summary>
	/// Verifies Windows-style case-variant source paths are de-duplicated.
	/// </summary>
	[Fact]
	public void Order_Edge_ShouldTreatCaseVariantPathsAsDuplicates_OnWindows()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		using TemporaryDirectory temporaryDirectory = new();
		string sharedSourcePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources", "shared")).FullName;
		string caseVariantSourcePath = PathTestUtilities.InvertPathCase(sharedSourcePath);

		SourceBranchOrderingService service = new();
		FakeSourcePriorityService priorityService = new(
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				["Preferred"] = 0,
				["Secondary"] = 1
			});

		IReadOnlyList<MergerfsSourceBranchCandidate> ordered = service.Order(
			[
				new MergerfsSourceBranchCandidate("Secondary", caseVariantSourcePath),
				new MergerfsSourceBranchCandidate("Preferred", sharedSourcePath)
			],
			priorityService);

		MergerfsSourceBranchCandidate only = Assert.Single(ordered);
		Assert.Equal("Preferred", only.SourceName);
	}

	/// <summary>
	/// Verifies case-equivalent paths with matching priority and source name still select one deterministic winner.
	/// </summary>
	[Fact]
	public void Order_Edge_ShouldUseOrdinalPathTieBreak_ForCaseEquivalentPaths_OnWindows()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		using TemporaryDirectory temporaryDirectory = new();
		string sharedSourcePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources", "shared")).FullName;
		string upperCasePath = sharedSourcePath.ToUpperInvariant();
		string lowerCasePath = sharedSourcePath.ToLowerInvariant();

		SourceBranchOrderingService service = new();
		FakeSourcePriorityService priorityService = new(new Dictionary<string, int>(StringComparer.Ordinal));

		IReadOnlyList<MergerfsSourceBranchCandidate> ordered = service.Order(
			[
				new MergerfsSourceBranchCandidate("Same Source", upperCasePath),
				new MergerfsSourceBranchCandidate("Same Source", lowerCasePath)
			],
			priorityService);

		string expectedWinner = string.Compare(upperCasePath, lowerCasePath, StringComparison.Ordinal) <= 0
			? Path.GetFullPath(upperCasePath)
			: Path.GetFullPath(lowerCasePath);

		MergerfsSourceBranchCandidate selected = Assert.Single(ordered);
		Assert.Equal(expectedWinner, selected.SourcePath);
	}

	/// <summary>
	/// Verifies null and malformed input arguments are rejected.
	/// </summary>
	[Fact]
	public void Order_Failure_ShouldThrow_WhenInputsAreInvalid()
	{
		SourceBranchOrderingService service = new();
		FakeSourcePriorityService priorityService = new(new Dictionary<string, int>(StringComparer.Ordinal));

		Assert.Throws<ArgumentNullException>(() => service.Order(null!, priorityService));
		Assert.Throws<ArgumentNullException>(() => service.Order([], null!));
		Assert.Throws<ArgumentException>(() => service.Order([null!], priorityService));
	}

	/// <summary>
	/// Test double source-priority service used to make ordering tests deterministic.
	/// </summary>
	private sealed class FakeSourcePriorityService : ISourcePriorityService
	{
		private readonly IReadOnlyDictionary<string, int> _priorities;

		public FakeSourcePriorityService(IReadOnlyDictionary<string, int> priorities)
		{
			_priorities = priorities ?? throw new ArgumentNullException(nameof(priorities));
		}

		public int GetPriorityOrDefault(string sourceName, int unknownPriority = int.MaxValue)
		{
			ArgumentNullException.ThrowIfNull(sourceName);

			return _priorities.TryGetValue(sourceName, out int priority)
				? priority
				: unknownPriority;
		}

		public bool TryGetPriority(string sourceName, out int priority)
		{
			ArgumentNullException.ThrowIfNull(sourceName);

			if (_priorities.TryGetValue(sourceName, out int foundPriority))
			{
				priority = foundPriority;
				return true;
			}

			priority = int.MaxValue;
			return false;
		}
	}
}
