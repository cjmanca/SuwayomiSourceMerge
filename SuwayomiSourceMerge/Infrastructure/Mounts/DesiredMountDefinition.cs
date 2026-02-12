namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Describes one desired mount target and its desired identity/payload.
/// </summary>
internal sealed class DesiredMountDefinition
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DesiredMountDefinition"/> class.
	/// </summary>
	/// <param name="mountPoint">Desired absolute mountpoint path.</param>
	/// <param name="desiredIdentity">Desired identity token (for example fsname/hash token).</param>
	/// <param name="mountPayload">Payload required to execute a mount action (for example branch string).</param>
	/// <exception cref="ArgumentException">Thrown when required values are null, empty, or whitespace.</exception>
	public DesiredMountDefinition(
		string mountPoint,
		string desiredIdentity,
		string mountPayload)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mountPoint);
		ArgumentException.ThrowIfNullOrWhiteSpace(desiredIdentity);
		ArgumentException.ThrowIfNullOrWhiteSpace(mountPayload);

		MountPoint = mountPoint;
		DesiredIdentity = desiredIdentity;
		MountPayload = mountPayload;
	}

	/// <summary>
	/// Gets the desired mountpoint path.
	/// </summary>
	public string MountPoint
	{
		get;
	}

	/// <summary>
	/// Gets the desired identity token.
	/// </summary>
	public string DesiredIdentity
	{
		get;
	}

	/// <summary>
	/// Gets the payload used to execute a mount/remount action.
	/// </summary>
	public string MountPayload
	{
		get;
	}
}
