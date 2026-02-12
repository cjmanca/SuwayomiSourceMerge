namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="MountReconciliationService"/>.
/// </summary>
public sealed class MountReconciliationServiceTests
{
	/// <summary>
	/// Verifies no actions are emitted when desired and actual mount state already match.
	/// </summary>
	[Fact]
	public void Reconcile_Expected_ShouldReturnNoActions_WhenDesiredAndActualStateMatch()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts:
			[
				new DesiredMountDefinition("/ssm/merged/Title", "suwayomi_hash", "branch-spec")
			],
			actualEntries:
			[
				new MountSnapshotEntry("/ssm/merged/Title", "fuse.mergerfs", "suwayomi_hash", "rw", isHealthy: true)
			],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: true,
			forceRemountMountPoints: []);

		MountReconciliationPlan plan = service.Reconcile(input);

		Assert.Empty(plan.Actions);
	}

	/// <summary>
	/// Verifies desired and actual mountpoints that differ only by trailing separator are treated as equivalent.
	/// </summary>
	[Fact]
	public void Reconcile_Edge_ShouldTreatTrailingSeparatorVariantsAsEquivalent()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts:
			[
				new DesiredMountDefinition("/ssm/merged/Title/", "suwayomi_hash", "branch-spec")
			],
			actualEntries:
			[
				new MountSnapshotEntry("/ssm/merged/Title", "fuse.mergerfs", "suwayomi_hash", "rw", isHealthy: true)
			],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: true,
			forceRemountMountPoints: []);

		MountReconciliationPlan plan = service.Reconcile(input);

		Assert.Empty(plan.Actions);
	}

	/// <summary>
	/// Verifies desired and actual mountpoints that differ only by slash style are treated as equivalent.
	/// </summary>
	[Fact]
	public void Reconcile_Edge_ShouldTreatSlashStyleVariantsAsEquivalent()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts:
			[
				new DesiredMountDefinition("\\ssm\\merged\\Title", "suwayomi_hash", "branch-spec")
			],
			actualEntries:
			[
				new MountSnapshotEntry("/ssm/merged/Title", "fuse.mergerfs", "suwayomi_hash", "rw", isHealthy: true)
			],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: true,
			forceRemountMountPoints: []);

		MountReconciliationPlan plan = service.Reconcile(input);

		Assert.Empty(plan.Actions);
	}

	/// <summary>
	/// Verifies missing desired mountpoints produce mount actions.
	/// </summary>
	[Fact]
	public void Reconcile_Expected_ShouldPlanMount_WhenDesiredMountIsMissing()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts:
			[
				new DesiredMountDefinition("/ssm/merged/Title", "suwayomi_hash", "branch-spec")
			],
			actualEntries: [],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: false,
			forceRemountMountPoints: []);

		MountReconciliationPlan plan = service.Reconcile(input);

		Assert.Single(plan.Actions);
		AssertAction(
			plan.Actions[0],
			MountReconciliationActionKind.Mount,
			"/ssm/merged/Title",
			MountReconciliationReason.MissingMount,
			"suwayomi_hash",
			"branch-spec");
	}

	/// <summary>
	/// Verifies non-mergerfs mounts at desired targets produce remount actions.
	/// </summary>
	[Fact]
	public void Reconcile_Expected_ShouldPlanRemount_WhenTargetHasNonMergerfsMount()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts:
			[
				new DesiredMountDefinition("/ssm/merged/Title", "suwayomi_hash", "branch-spec")
			],
			actualEntries:
			[
				new MountSnapshotEntry("/ssm/merged/Title", "ext4", "disk-source", "rw", isHealthy: true)
			],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: false,
			forceRemountMountPoints: []);

		MountReconciliationPlan plan = service.Reconcile(input);

		Assert.Single(plan.Actions);
		AssertAction(
			plan.Actions[0],
			MountReconciliationActionKind.Remount,
			"/ssm/merged/Title",
			MountReconciliationReason.NonMergerfsAtTarget,
			"suwayomi_hash",
			"branch-spec");
	}

	/// <summary>
	/// Verifies identity mismatches produce remount actions.
	/// </summary>
	[Fact]
	public void Reconcile_Expected_ShouldPlanRemount_WhenDesiredIdentityDiffers()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts:
			[
				new DesiredMountDefinition("/ssm/merged/Title", "suwayomi_new", "branch-spec")
			],
			actualEntries:
			[
				new MountSnapshotEntry("/ssm/merged/Title", "fuse.mergerfs", string.Empty, "rw,fsname=suwayomi_old", isHealthy: true)
			],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: false,
			forceRemountMountPoints: []);

		MountReconciliationPlan plan = service.Reconcile(input);

		Assert.Single(plan.Actions);
		AssertAction(
			plan.Actions[0],
			MountReconciliationActionKind.Remount,
			"/ssm/merged/Title",
			MountReconciliationReason.DesiredIdentityMismatch,
			"suwayomi_new",
			"branch-spec");
	}

	/// <summary>
	/// Verifies unhealthy mergerfs mounts produce remount actions when health checks are enabled.
	/// </summary>
	[Fact]
	public void Reconcile_Edge_ShouldPlanRemount_WhenMountIsUnhealthyAndHealthChecksEnabled()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts:
			[
				new DesiredMountDefinition("/ssm/merged/Title", "suwayomi_hash", "branch-spec")
			],
			actualEntries:
			[
				new MountSnapshotEntry("/ssm/merged/Title", "fuse.mergerfs", "suwayomi_hash", "rw", isHealthy: false)
			],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: true,
			forceRemountMountPoints: []);

		MountReconciliationPlan plan = service.Reconcile(input);

		Assert.Single(plan.Actions);
		AssertAction(
			plan.Actions[0],
			MountReconciliationActionKind.Remount,
			"/ssm/merged/Title",
			MountReconciliationReason.UnhealthyMount,
			"suwayomi_hash",
			"branch-spec");
	}

	/// <summary>
	/// Verifies stale mergerfs mounts under managed roots produce unmount actions.
	/// </summary>
	[Fact]
	public void Reconcile_Edge_ShouldPlanUnmount_WhenStaleMergerfsMountExistsUnderManagedRoot()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts:
			[
				new DesiredMountDefinition("/ssm/merged/Keep", "keep_hash", "keep-spec")
			],
			actualEntries:
			[
				new MountSnapshotEntry("/ssm/merged/Keep", "fuse.mergerfs", "keep_hash", "rw", isHealthy: true),
				new MountSnapshotEntry("/ssm/merged/Stale", "fuse.mergerfs", "stale_hash", "rw", isHealthy: true)
			],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: false,
			forceRemountMountPoints: []);

		MountReconciliationPlan plan = service.Reconcile(input);

		Assert.Single(plan.Actions);
		AssertAction(
			plan.Actions[0],
			MountReconciliationActionKind.Unmount,
			"/ssm/merged/Stale",
			MountReconciliationReason.StaleMount,
			desiredIdentity: null,
			mountPayload: null);
	}

	/// <summary>
	/// Verifies stale non-mergerfs mounts are ignored by stale cleanup.
	/// </summary>
	[Fact]
	public void Reconcile_Edge_ShouldIgnoreStaleNonMergerfsMounts()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts: [],
			actualEntries:
			[
				new MountSnapshotEntry("/ssm/merged/Stale", "ext4", "stale_disk", "rw", isHealthy: true)
			],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: false,
			forceRemountMountPoints: []);

		MountReconciliationPlan plan = service.Reconcile(input);

		Assert.Empty(plan.Actions);
	}

	/// <summary>
	/// Verifies stale unmount actions are ordered by descending path depth and then ordinal path text.
	/// </summary>
	[Fact]
	public void Reconcile_Edge_ShouldOrderStaleUnmountsByDepthThenOrdinal()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts: [],
			actualEntries:
			[
				new MountSnapshotEntry("/ssm/merged/B", "fuse.mergerfs", "b", "rw", isHealthy: true),
				new MountSnapshotEntry("/ssm/merged/A/Inner", "fuse.mergerfs", "a-inner", "rw", isHealthy: true),
				new MountSnapshotEntry("/ssm/merged/A", "fuse.mergerfs", "a", "rw", isHealthy: true)
			],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: false,
			forceRemountMountPoints: []);

		MountReconciliationPlan plan = service.Reconcile(input);

		Assert.Equal(
			[
				"/ssm/merged/A/Inner",
				"/ssm/merged/A",
				"/ssm/merged/B"
			],
			plan.Actions.Select(action => action.MountPoint).ToArray());
		Assert.All(plan.Actions, action => Assert.Equal(MountReconciliationActionKind.Unmount, action.Kind));
	}

	/// <summary>
	/// Verifies force-remount flags take precedence over identity/health matching.
	/// </summary>
	[Fact]
	public void Reconcile_Edge_ShouldPlanForcedRemount_WhenMountPointIsForceFlagged()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts:
			[
				new DesiredMountDefinition("/ssm/merged/Title", "suwayomi_hash", "branch-spec")
			],
			actualEntries:
			[
				new MountSnapshotEntry("/ssm/merged/Title", "fuse.mergerfs", "suwayomi_hash", "rw", isHealthy: true)
			],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: true,
			forceRemountMountPoints: ["/ssm/merged/Title"]);

		MountReconciliationPlan plan = service.Reconcile(input);

		Assert.Single(plan.Actions);
		AssertAction(
			plan.Actions[0],
			MountReconciliationActionKind.Remount,
			"/ssm/merged/Title",
			MountReconciliationReason.ForcedRemount,
			"suwayomi_hash",
			"branch-spec");
	}

	/// <summary>
	/// Verifies force-remount mountpoint checks are normalized before comparison.
	/// </summary>
	[Fact]
	public void Reconcile_Edge_ShouldNormalizeForceRemountMountPointsBeforeComparison()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts:
			[
				new DesiredMountDefinition("/ssm/merged/Title", "suwayomi_hash", "branch-spec")
			],
			actualEntries:
			[
				new MountSnapshotEntry("/ssm/merged/Title/", "fuse.mergerfs", "suwayomi_hash", "rw", isHealthy: true)
			],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: true,
			forceRemountMountPoints: ["/ssm/merged/Title/"]);

		MountReconciliationPlan plan = service.Reconcile(input);

		Assert.Single(plan.Actions);
		AssertAction(
			plan.Actions[0],
			MountReconciliationActionKind.Remount,
			"/ssm/merged/Title",
			MountReconciliationReason.ForcedRemount,
			"suwayomi_hash",
			"branch-spec");
	}

	/// <summary>
	/// Verifies reconciliation action output is deterministic regardless of input list order.
	/// </summary>
	[Fact]
	public void Reconcile_Edge_ShouldRemainDeterministic_WhenInputOrderingVaries()
	{
		MountReconciliationService service = new();
		DesiredMountDefinition desiredA = new("/ssm/merged/Beta", "beta_hash", "beta-spec");
		DesiredMountDefinition desiredB = new("/ssm/merged/Alpha", "alpha_hash", "alpha-spec");
		MountSnapshotEntry actualA = new("/ssm/merged/Alpha", "fuse.mergerfs", "old_alpha_hash", "rw", isHealthy: true);
		MountSnapshotEntry actualB = new("/ssm/merged/Zeta/Stale", "fuse.mergerfs", "stale", "rw", isHealthy: true);

		MountReconciliationInput firstInput = CreateInput(
			desiredMounts: [desiredA, desiredB],
			actualEntries: [actualA, actualB],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: false,
			forceRemountMountPoints: []);
		MountReconciliationInput secondInput = CreateInput(
			desiredMounts: [desiredB, desiredA],
			actualEntries: [actualB, actualA],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: false,
			forceRemountMountPoints: []);

		MountReconciliationPlan firstPlan = service.Reconcile(firstInput);
		MountReconciliationPlan secondPlan = service.Reconcile(secondInput);

		Assert.Equal(
			firstPlan.Actions.Select(SerializeAction).ToArray(),
			secondPlan.Actions.Select(SerializeAction).ToArray());
	}

	/// <summary>
	/// Verifies deterministic reconciliation ordering remains stable with mixed slash styles and trailing separators.
	/// </summary>
	[Fact]
	public void Reconcile_Edge_ShouldRemainDeterministic_WhenNormalizedEquivalentPathStylesVary()
	{
		MountReconciliationService service = new();
		DesiredMountDefinition desiredA = new("/ssm/merged/Beta/", "beta_hash", "beta-spec");
		DesiredMountDefinition desiredB = new("\\ssm\\merged\\Alpha", "alpha_hash", "alpha-spec");
		MountSnapshotEntry actualA = new("/ssm/merged/Alpha/", "fuse.mergerfs", "old_alpha_hash", "rw", isHealthy: true);
		MountSnapshotEntry actualB = new("\\ssm\\merged\\Zeta\\Stale\\", "fuse.mergerfs", "stale", "rw", isHealthy: true);

		MountReconciliationInput firstInput = CreateInput(
			desiredMounts: [desiredA, desiredB],
			actualEntries: [actualA, actualB],
			managedMountRoots: ["\\ssm\\merged\\"],
			enableHealthChecks: false,
			forceRemountMountPoints: []);
		MountReconciliationInput secondInput = CreateInput(
			desiredMounts: [desiredB, desiredA],
			actualEntries: [actualB, actualA],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: false,
			forceRemountMountPoints: []);

		MountReconciliationPlan firstPlan = service.Reconcile(firstInput);
		MountReconciliationPlan secondPlan = service.Reconcile(secondInput);

		Assert.Equal(
			firstPlan.Actions.Select(SerializeAction).ToArray(),
			secondPlan.Actions.Select(SerializeAction).ToArray());
	}

	/// <summary>
	/// Verifies null input guard behavior.
	/// </summary>
	[Fact]
	public void Reconcile_Failure_ShouldThrow_WhenInputIsNull()
	{
		MountReconciliationService service = new();

		Assert.Throws<ArgumentNullException>(() => service.Reconcile(null!));
	}

	/// <summary>
	/// Verifies duplicate desired mountpoints are rejected.
	/// </summary>
	[Fact]
	public void Reconcile_Failure_ShouldThrow_WhenDesiredMountPointsContainDuplicates()
	{
		MountReconciliationService service = new();
		MountReconciliationInput input = CreateInput(
			desiredMounts:
			[
				new DesiredMountDefinition("/ssm/merged/Title", "hash-a", "spec-a"),
				new DesiredMountDefinition("/ssm/merged/Title/", "hash-b", "spec-b")
			],
			actualEntries: [],
			managedMountRoots: ["/ssm/merged"],
			enableHealthChecks: false,
			forceRemountMountPoints: []);

		Assert.Throws<ArgumentException>(() => service.Reconcile(input));
	}

	/// <summary>
	/// Builds an input model for reconciliation tests.
	/// </summary>
	/// <param name="desiredMounts">Desired mount definitions.</param>
	/// <param name="actualEntries">Actual mount entries.</param>
	/// <param name="managedMountRoots">Managed roots used for stale cleanup checks.</param>
	/// <param name="enableHealthChecks">Whether health check reconciliation is enabled.</param>
	/// <param name="forceRemountMountPoints">Mountpoints to force-remount.</param>
	/// <returns>Constructed reconciliation input model.</returns>
	private static MountReconciliationInput CreateInput(
		IReadOnlyList<DesiredMountDefinition> desiredMounts,
		IReadOnlyList<MountSnapshotEntry> actualEntries,
		IReadOnlyList<string> managedMountRoots,
		bool enableHealthChecks,
		IEnumerable<string> forceRemountMountPoints)
	{
		HashSet<string> forceRemountSet = new(forceRemountMountPoints, StringComparer.Ordinal);

		return new MountReconciliationInput(
			desiredMounts,
			new MountSnapshot(actualEntries, []),
			managedMountRoots,
			enableHealthChecks,
			forceRemountSet);
	}

	/// <summary>
	/// Serializes action values into a deterministic string for equality assertions.
	/// </summary>
	/// <param name="action">Action to serialize.</param>
	/// <returns>Serialized action representation.</returns>
	private static string SerializeAction(MountReconciliationAction action)
	{
		return $"{action.Kind}|{action.MountPoint}|{action.Reason}|{action.DesiredIdentity}|{action.MountPayload}";
	}

	/// <summary>
	/// Asserts one reconciliation action matches expected values.
	/// </summary>
	/// <param name="action">Action under test.</param>
	/// <param name="kind">Expected kind.</param>
	/// <param name="mountPoint">Expected mountpoint.</param>
	/// <param name="reason">Expected reason.</param>
	/// <param name="desiredIdentity">Expected desired identity.</param>
	/// <param name="mountPayload">Expected mount payload.</param>
	private static void AssertAction(
		MountReconciliationAction action,
		MountReconciliationActionKind kind,
		string mountPoint,
		MountReconciliationReason reason,
		string? desiredIdentity,
		string? mountPayload)
	{
		Assert.Equal(kind, action.Kind);
		Assert.Equal(mountPoint, action.MountPoint);
		Assert.Equal(reason, action.Reason);
		Assert.Equal(desiredIdentity, action.DesiredIdentity);
		Assert.Equal(mountPayload, action.MountPayload);
	}
}
