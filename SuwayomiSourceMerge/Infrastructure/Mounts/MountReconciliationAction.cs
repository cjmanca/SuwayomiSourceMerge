namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Represents one reconciliation action.
/// </summary>
internal sealed class MountReconciliationAction
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MountReconciliationAction"/> class.
	/// </summary>
	/// <param name="kind">Action kind.</param>
	/// <param name="mountPoint">Action target mountpoint.</param>
	/// <param name="desiredIdentity">Optional desired identity token.</param>
	/// <param name="mountPayload">Optional mount payload.</param>
	/// <param name="reason">Reason for producing the action.</param>
	/// <exception cref="ArgumentException">Thrown when required values are missing for the selected <paramref name="kind"/>.</exception>
	public MountReconciliationAction(
		MountReconciliationActionKind kind,
		string mountPoint,
		string? desiredIdentity,
		string? mountPayload,
		MountReconciliationReason reason)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mountPoint);

		if (kind == MountReconciliationActionKind.Unmount)
		{
			if (desiredIdentity is not null && string.IsNullOrWhiteSpace(desiredIdentity))
			{
				throw new ArgumentException(
					"Desired identity must be null or non-empty when action kind is Unmount.",
					nameof(desiredIdentity));
			}

			if (mountPayload is not null && string.IsNullOrWhiteSpace(mountPayload))
			{
				throw new ArgumentException(
					"Mount payload must be null or non-empty when action kind is Unmount.",
					nameof(mountPayload));
			}
		}
		else
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(desiredIdentity);
			ArgumentException.ThrowIfNullOrWhiteSpace(mountPayload);
		}

		Kind = kind;
		MountPoint = mountPoint;
		DesiredIdentity = desiredIdentity;
		MountPayload = mountPayload;
		Reason = reason;
	}

	/// <summary>
	/// Gets the action kind.
	/// </summary>
	public MountReconciliationActionKind Kind
	{
		get;
	}

	/// <summary>
	/// Gets the action target mountpoint.
	/// </summary>
	public string MountPoint
	{
		get;
	}

	/// <summary>
	/// Gets the desired identity token, when applicable.
	/// </summary>
	public string? DesiredIdentity
	{
		get;
	}

	/// <summary>
	/// Gets the mount payload, when applicable.
	/// </summary>
	public string? MountPayload
	{
		get;
	}

	/// <summary>
	/// Gets the reason the action was produced.
	/// </summary>
	public MountReconciliationReason Reason
	{
		get;
	}
}

/// <summary>
/// Defines reconciliation action kinds.
/// </summary>
internal enum MountReconciliationActionKind
{
	/// <summary>
	/// Create a missing mount.
	/// </summary>
	Mount,

	/// <summary>
	/// Replace an existing mount with the desired mount.
	/// </summary>
	Remount,

	/// <summary>
	/// Remove a stale mount.
	/// </summary>
	Unmount
}
