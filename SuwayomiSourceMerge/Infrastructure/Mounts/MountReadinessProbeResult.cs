namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Represents timeout-bounded readiness probe results for one mounted path.
/// </summary>
/// <param name="IsReady">Whether probe succeeded and path appears ready.</param>
/// <param name="Diagnostic">Diagnostic text describing probe outcome.</param>
internal readonly record struct MountReadinessProbeResult(
	bool IsReady,
	string Diagnostic)
{
	/// <summary>
	/// Creates a successful readiness result.
	/// </summary>
	/// <param name="diagnostic">Diagnostic text.</param>
	/// <returns>Successful readiness result.</returns>
	public static MountReadinessProbeResult Ready(string diagnostic)
	{
		return new MountReadinessProbeResult(true, diagnostic);
	}

	/// <summary>
	/// Creates a failed readiness result.
	/// </summary>
	/// <param name="diagnostic">Diagnostic text.</param>
	/// <returns>Failed readiness result.</returns>
	public static MountReadinessProbeResult NotReady(string diagnostic)
	{
		return new MountReadinessProbeResult(false, diagnostic);
	}
}
