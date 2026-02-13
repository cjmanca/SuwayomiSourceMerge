using SuwayomiSourceMerge.Configuration.Resolution;

namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Builds deterministic branch-link plans that combine override and source branches for mergerfs mounts.
/// </summary>
internal sealed class MergerfsBranchPlanningService : IMergerfsBranchPlanningService
{
	/// <summary>
	/// Mergerfs read-write mode token.
	/// </summary>
	private const string READ_WRITE_MODE_TOKEN = "RW";

	/// <summary>
	/// Mergerfs read-only mode token.
	/// </summary>
	private const string READ_ONLY_MODE_TOKEN = "RO";

	/// <summary>
	/// Delimiter used between branch specifications.
	/// </summary>
	private const char BRANCH_SPECIFICATION_DELIMITER = ':';

	/// <summary>
	/// Source-priority service used for deterministic source ordering.
	/// </summary>
	private readonly ISourcePriorityService _sourcePriorityService;

	/// <summary>
	/// Override branch selection service.
	/// </summary>
	private readonly OverrideBranchSelectionService _overrideBranchSelectionService;

	/// <summary>
	/// Source branch ordering service.
	/// </summary>
	private readonly SourceBranchOrderingService _sourceBranchOrderingService;

	/// <summary>
	/// Branch-link naming policy service.
	/// </summary>
	private readonly BranchLinkNamingPolicy _branchLinkNamingPolicy;

	/// <summary>
	/// Branch identity service.
	/// </summary>
	private readonly BranchIdentityService _branchIdentityService;

	/// <summary>
	/// Initializes a new instance of the <see cref="MergerfsBranchPlanningService"/> class.
	/// </summary>
	/// <param name="sourcePriorityService">Source-priority service used for deterministic source ordering.</param>
	public MergerfsBranchPlanningService(ISourcePriorityService sourcePriorityService)
		: this(
			sourcePriorityService,
			new OverrideBranchSelectionService(),
			new SourceBranchOrderingService(),
			new BranchLinkNamingPolicy(),
			new BranchIdentityService())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MergerfsBranchPlanningService"/> class.
	/// </summary>
	/// <param name="sourcePriorityService">Source-priority service used for deterministic source ordering.</param>
	/// <param name="overrideBranchSelectionService">Override branch selection service.</param>
	/// <param name="sourceBranchOrderingService">Source branch ordering service.</param>
	/// <param name="branchLinkNamingPolicy">Branch-link naming policy service.</param>
	/// <param name="branchIdentityService">Branch identity service.</param>
	internal MergerfsBranchPlanningService(
		ISourcePriorityService sourcePriorityService,
		OverrideBranchSelectionService overrideBranchSelectionService,
		SourceBranchOrderingService sourceBranchOrderingService,
		BranchLinkNamingPolicy branchLinkNamingPolicy,
		BranchIdentityService branchIdentityService)
	{
		_sourcePriorityService = sourcePriorityService ?? throw new ArgumentNullException(nameof(sourcePriorityService));
		_overrideBranchSelectionService = overrideBranchSelectionService ?? throw new ArgumentNullException(nameof(overrideBranchSelectionService));
		_sourceBranchOrderingService = sourceBranchOrderingService ?? throw new ArgumentNullException(nameof(sourceBranchOrderingService));
		_branchLinkNamingPolicy = branchLinkNamingPolicy ?? throw new ArgumentNullException(nameof(branchLinkNamingPolicy));
		_branchIdentityService = branchIdentityService ?? throw new ArgumentNullException(nameof(branchIdentityService));
	}

	/// <inheritdoc />
	public MergerfsBranchPlan Plan(MergerfsBranchPlanningRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		OverrideBranchSelectionResult overrideSelection = _overrideBranchSelectionService.Select(
			request.CanonicalTitle,
			request.OverrideVolumePaths);

		IReadOnlyList<MergerfsSourceBranchCandidate> orderedSourceBranches = _sourceBranchOrderingService.Order(
			request.SourceBranches,
			_sourcePriorityService);

		string groupId = _branchIdentityService.BuildGroupId(request.GroupKey);
		string branchDirectoryPath = Path.GetFullPath(Path.Combine(request.BranchLinksRootPath, groupId));

		List<MergerfsBranchLinkDefinition> branchLinks = BuildBranchLinkDefinitions(
			overrideSelection.OrderedEntries,
			orderedSourceBranches,
			branchDirectoryPath);

		string branchSpecification = BuildBranchSpecification(branchLinks);
		string desiredIdentity = _branchIdentityService.BuildDesiredIdentity(request.GroupKey, branchSpecification);

		return new MergerfsBranchPlan(
			overrideSelection.PreferredOverridePath,
			branchDirectoryPath,
			branchSpecification,
			desiredIdentity,
			groupId,
			branchLinks);
	}

	/// <summary>
	/// Builds ordered branch-link definitions with override entries first and source entries last.
	/// </summary>
	/// <param name="overrideEntries">Ordered override entries.</param>
	/// <param name="orderedSourceBranches">Ordered source branches.</param>
	/// <param name="branchDirectoryPath">Absolute branch-directory path.</param>
	/// <returns>Ordered branch-link definitions.</returns>
	private List<MergerfsBranchLinkDefinition> BuildBranchLinkDefinitions(
		IReadOnlyList<OverrideBranchSelectionEntry> overrideEntries,
		IReadOnlyList<MergerfsSourceBranchCandidate> orderedSourceBranches,
		string branchDirectoryPath)
	{
		List<MergerfsBranchLinkDefinition> branchLinks = [];

		OverrideBranchSelectionEntry preferredOverrideEntry = overrideEntries[0];
		string primaryOverrideLinkName = _branchLinkNamingPolicy.BuildPrimaryOverrideLinkName();
		branchLinks.Add(
			CreateBranchLinkDefinition(
				primaryOverrideLinkName,
				branchDirectoryPath,
				preferredOverrideEntry.TitlePath,
				MergerfsBranchAccessMode.ReadWrite));

		int additionalOverrideIndex = 0;
		for (int index = 1; index < overrideEntries.Count; index++)
		{
			OverrideBranchSelectionEntry overrideEntry = overrideEntries[index];
			string overrideLinkName = _branchLinkNamingPolicy.BuildAdditionalOverrideLinkName(
				overrideEntry.VolumeRootPath,
				additionalOverrideIndex);

			branchLinks.Add(
				CreateBranchLinkDefinition(
					overrideLinkName,
					branchDirectoryPath,
					overrideEntry.TitlePath,
					MergerfsBranchAccessMode.ReadWrite));

			additionalOverrideIndex++;
		}

		for (int index = 0; index < orderedSourceBranches.Count; index++)
		{
			MergerfsSourceBranchCandidate sourceBranch = orderedSourceBranches[index];
			string sourceLinkName = _branchLinkNamingPolicy.BuildSourceLinkName(sourceBranch.SourceName, index);
			branchLinks.Add(
				CreateBranchLinkDefinition(
					sourceLinkName,
					branchDirectoryPath,
					sourceBranch.SourcePath,
					MergerfsBranchAccessMode.ReadOnly));
		}

		return branchLinks;
	}

	/// <summary>
	/// Creates one branch-link definition under the given branch directory.
	/// </summary>
	/// <param name="linkName">Filesystem-safe link name.</param>
	/// <param name="branchDirectoryPath">Absolute branch directory path.</param>
	/// <param name="targetPath">Absolute target path.</param>
	/// <param name="accessMode">Branch access mode.</param>
	/// <returns>Created branch-link definition.</returns>
	private static MergerfsBranchLinkDefinition CreateBranchLinkDefinition(
		string linkName,
		string branchDirectoryPath,
		string targetPath,
		MergerfsBranchAccessMode accessMode)
	{
		string linkPath = Path.GetFullPath(Path.Combine(branchDirectoryPath, linkName));
		string safeLinkPath = PathSafetyPolicy.EnsureStrictChildPath(
			branchDirectoryPath,
			linkPath,
			nameof(linkName));
		return new MergerfsBranchLinkDefinition(linkName, safeLinkPath, targetPath, accessMode);
	}

	/// <summary>
	/// Builds a mergerfs branch specification from ordered branch-link definitions.
	/// </summary>
	/// <param name="branchLinks">Ordered branch-link definitions.</param>
	/// <returns>Deterministic mergerfs branch specification string.</returns>
	private static string BuildBranchSpecification(IReadOnlyList<MergerfsBranchLinkDefinition> branchLinks)
	{
		return string.Join(
			BRANCH_SPECIFICATION_DELIMITER,
			branchLinks.Select(
				link => $"{link.LinkPath}={ResolveModeToken(link.AccessMode)}"));
	}

	/// <summary>
	/// Resolves mergerfs branch mode token from access mode.
	/// </summary>
	/// <param name="accessMode">Access mode to map.</param>
	/// <returns>Mergerfs mode token.</returns>
	private static string ResolveModeToken(MergerfsBranchAccessMode accessMode)
	{
		return accessMode switch
		{
			MergerfsBranchAccessMode.ReadWrite => READ_WRITE_MODE_TOKEN,
			MergerfsBranchAccessMode.ReadOnly => READ_ONLY_MODE_TOKEN,
			_ => throw new ArgumentOutOfRangeException(
				nameof(accessMode),
				accessMode,
				"Unsupported mergerfs branch access mode.")
		};
	}
}
