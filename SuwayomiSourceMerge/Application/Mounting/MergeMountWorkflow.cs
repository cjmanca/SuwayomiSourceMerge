using System.Security.Cryptography;
using System.Text;

using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.Infrastructure.Volumes;

namespace SuwayomiSourceMerge.Application.Mounting;

/// <summary>
/// Implements production merge mount orchestration and runtime lifecycle cleanup hooks.
/// </summary>
internal sealed partial class MergeMountWorkflow : IMergeMountWorkflow, IMergeRuntimeLifecycle
{
	/// <summary>Event id emitted when one merge pass starts.</summary>
	private const string MergePassStartedEvent = "merge.workflow.started";

	/// <summary>Event id emitted when one merge pass finishes.</summary>
	private const string MergePassCompletedEvent = "merge.workflow.completed";

	/// <summary>Event id emitted for merge pass warnings.</summary>
	private const string MergePassWarningEvent = "merge.workflow.warning";

	/// <summary>Event id emitted for merge pass errors.</summary>
	private const string MergePassErrorEvent = "merge.workflow.error";

	/// <summary>Event id emitted when one reconciliation action is applied.</summary>
	private const string MergeActionEvent = "merge.workflow.action";

	/// <summary>Event id emitted for lifecycle cleanup runs.</summary>
	private const string MergeCleanupEvent = "merge.workflow.cleanup";

	/// <summary>
	/// Path comparison mode used by mountpoint containment checks.
	/// </summary>
	private static readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
		? StringComparison.OrdinalIgnoreCase
		: StringComparison.Ordinal;

	/// <summary>
	/// Workflow options.
	/// </summary>
	private readonly MergeMountWorkflowOptions _options;

	/// <summary>
	/// Manga equivalence resolver.
	/// </summary>
	private readonly IMangaEquivalenceService _mangaEquivalenceService;

	/// <summary>
	/// Scene tag matcher.
	/// </summary>
	private readonly ISceneTagMatcher _sceneTagMatcher;

	/// <summary>
	/// Title normalizer used for grouping.
	/// </summary>
	private readonly ITitleComparisonNormalizer _titleComparisonNormalizer;

	/// <summary>
	/// Volume discovery service.
	/// </summary>
	private readonly IContainerVolumeDiscoveryService _volumeDiscoveryService;

	/// <summary>
	/// Branch planning service.
	/// </summary>
	private readonly IMergerfsBranchPlanningService _branchPlanningService;

	/// <summary>
	/// Mount snapshot service.
	/// </summary>
	private readonly IMountSnapshotService _mountSnapshotService;

	/// <summary>
	/// Mount reconciliation service.
	/// </summary>
	private readonly IMountReconciliationService _mountReconciliationService;

	/// <summary>
	/// Mount command service.
	/// </summary>
	private readonly IMergerfsMountCommandService _mountCommandService;

	/// <summary>
	/// Branch link staging service.
	/// </summary>
	private readonly IBranchLinkStagingService _branchLinkStagingService;

	/// <summary>
	/// Override details service.
	/// </summary>
	private readonly IOverrideDetailsService _overrideDetailsService;

	/// <summary>
	/// Logger dependency.
	/// </summary>
	private readonly ISsmLogger _logger;

	/// <summary>
	/// Normalized excluded source-name set.
	/// </summary>
	private readonly HashSet<string> _excludedSources;

	/// <summary>
	/// Synchronization gate for last desired branch directory cache.
	/// </summary>
	private readonly object _syncRoot = new();

	/// <summary>
	/// Last desired branch directories from the most recent merge pass.
	/// </summary>
	private HashSet<string> _lastDesiredBranchDirectories = new(PathSafetyPolicy.GetPathComparer());

	/// <summary>
	/// Last desired branch directory mapping by normalized mountpoint from the most recent merge pass.
	/// </summary>
	private Dictionary<string, string> _lastBranchDirectoryByMountPoint = new(PathSafetyPolicy.GetPathComparer());

	/// <summary>
	/// Initializes a new instance of the <see cref="MergeMountWorkflow"/> class.
	/// </summary>
	/// <param name="options">Workflow options.</param>
	/// <param name="mangaEquivalenceService">Manga equivalence resolver.</param>
	/// <param name="sceneTagMatcher">Scene-tag matcher.</param>
	/// <param name="volumeDiscoveryService">Volume discovery service.</param>
	/// <param name="branchPlanningService">Branch planning service.</param>
	/// <param name="mountSnapshotService">Mount snapshot service.</param>
	/// <param name="mountReconciliationService">Mount reconciliation service.</param>
	/// <param name="mountCommandService">Mount command service.</param>
	/// <param name="branchLinkStagingService">Branch link staging service.</param>
	/// <param name="overrideDetailsService">Override details service.</param>
	/// <param name="logger">Logger dependency.</param>
	public MergeMountWorkflow(
		MergeMountWorkflowOptions options,
		IMangaEquivalenceService mangaEquivalenceService,
		ISceneTagMatcher sceneTagMatcher,
		IContainerVolumeDiscoveryService volumeDiscoveryService,
		IMergerfsBranchPlanningService branchPlanningService,
		IMountSnapshotService mountSnapshotService,
		IMountReconciliationService mountReconciliationService,
		IMergerfsMountCommandService mountCommandService,
		IBranchLinkStagingService branchLinkStagingService,
		IOverrideDetailsService overrideDetailsService,
		ISsmLogger logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_mangaEquivalenceService = mangaEquivalenceService ?? throw new ArgumentNullException(nameof(mangaEquivalenceService));
		_sceneTagMatcher = sceneTagMatcher ?? throw new ArgumentNullException(nameof(sceneTagMatcher));
		_volumeDiscoveryService = volumeDiscoveryService ?? throw new ArgumentNullException(nameof(volumeDiscoveryService));
		_branchPlanningService = branchPlanningService ?? throw new ArgumentNullException(nameof(branchPlanningService));
		_mountSnapshotService = mountSnapshotService ?? throw new ArgumentNullException(nameof(mountSnapshotService));
		_mountReconciliationService = mountReconciliationService ?? throw new ArgumentNullException(nameof(mountReconciliationService));
		_mountCommandService = mountCommandService ?? throw new ArgumentNullException(nameof(mountCommandService));
		_branchLinkStagingService = branchLinkStagingService ?? throw new ArgumentNullException(nameof(branchLinkStagingService));
		_overrideDetailsService = overrideDetailsService ?? throw new ArgumentNullException(nameof(overrideDetailsService));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_titleComparisonNormalizer = TitleComparisonNormalizerProvider.Get(_sceneTagMatcher);
		_excludedSources = new HashSet<string>(StringComparer.Ordinal);
		for (int index = 0; index < _options.ExcludedSources.Count; index++)
		{
			string sourceName = _options.ExcludedSources[index];
			string sourceKey = SourceNameKeyNormalizer.NormalizeSourceKey(sourceName);
			if (string.IsNullOrWhiteSpace(sourceKey))
			{
				continue;
			}

			_excludedSources.Add(sourceKey);
		}
	}

	/// <inheritdoc />
	public void OnWorkerStarting(CancellationToken cancellationToken = default)
	{
		if (!_options.StartupCleanupEnabled)
		{
			return;
		}

		RunCleanupPass("startup", cancellationToken);
	}

	/// <inheritdoc />
	public void OnWorkerStopping(CancellationToken cancellationToken = default)
	{
		if (!_options.UnmountOnExit)
		{
			return;
		}

		RunCleanupPass("shutdown", cancellationToken);
	}

	/// <summary>
	/// Builds one immutable logging context dictionary.
	/// </summary>
	/// <param name="pairs">Context key/value pairs.</param>
	/// <returns>Context dictionary.</returns>
	private static IReadOnlyDictionary<string, string> BuildContext(params (string Key, string Value)[] pairs)
	{
		Dictionary<string, string> context = new(StringComparer.Ordinal);
		for (int index = 0; index < pairs.Length; index++)
		{
			(string key, string value) = pairs[index];
			if (!string.IsNullOrWhiteSpace(key))
			{
				context[key] = value;
			}
		}

		return context;
	}

	/// <summary>
	/// Computes path depth used for deterministic deepest-first unmount ordering.
	/// </summary>
	/// <param name="path">Path to inspect.</param>
	/// <returns>Path segment depth.</returns>
	private static int GetPathDepth(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return 0;
		}

		return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
			.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
			.Length;
	}

	/// <summary>
	/// Computes short deterministic hash text.
	/// </summary>
	/// <param name="input">Input text.</param>
	/// <returns>Lowercase 16-hex-character hash prefix.</returns>
	private static string ComputeHash(string input)
	{
		byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return Convert.ToHexString(hash).ToLowerInvariant()[..16];
	}
}
