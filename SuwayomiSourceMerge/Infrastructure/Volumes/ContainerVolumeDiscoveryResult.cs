namespace SuwayomiSourceMerge.Infrastructure.Volumes;

/// <summary>
/// Represents the output of a container volume discovery pass.
/// </summary>
/// <remarks>
/// The constructor copies incoming collections so callers can safely reuse or mutate
/// their original lists without affecting the stored result.
/// </remarks>
internal sealed class ContainerVolumeDiscoveryResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ContainerVolumeDiscoveryResult"/> class.
	/// </summary>
	/// <param name="sourceVolumePaths">Discovered source volume directories.</param>
	/// <param name="overrideVolumePaths">Discovered override volume directories.</param>
	/// <param name="warnings">Non-fatal warnings produced while discovering volumes.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when any required collection argument is <see langword="null"/>.
	/// </exception>
	public ContainerVolumeDiscoveryResult(
		IReadOnlyList<string> sourceVolumePaths,
		IReadOnlyList<string> overrideVolumePaths,
		IReadOnlyList<ContainerVolumeDiscoveryWarning> warnings)
	{
		ArgumentNullException.ThrowIfNull(sourceVolumePaths);
		ArgumentNullException.ThrowIfNull(overrideVolumePaths);
		ArgumentNullException.ThrowIfNull(warnings);

		SourceVolumePaths = sourceVolumePaths.ToArray();
		OverrideVolumePaths = overrideVolumePaths.ToArray();
		Warnings = warnings.ToArray();
	}

	/// <summary>
	/// Gets the discovered source volume directories.
	/// </summary>
	public IReadOnlyList<string> SourceVolumePaths
	{
		get;
	}

	/// <summary>
	/// Gets the discovered override volume directories.
	/// </summary>
	public IReadOnlyList<string> OverrideVolumePaths
	{
		get;
	}

	/// <summary>
	/// Gets warnings generated during discovery, such as missing roots.
	/// </summary>
	public IReadOnlyList<ContainerVolumeDiscoveryWarning> Warnings
	{
		get;
	}
}

/// <summary>
/// Represents a non-fatal warning emitted during container volume discovery.
/// </summary>
internal sealed record ContainerVolumeDiscoveryWarning
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ContainerVolumeDiscoveryWarning"/> record.
	/// </summary>
	/// <param name="code">Stable warning code used for diagnostics and assertions.</param>
	/// <param name="rootPath">Root path associated with the warning.</param>
	/// <param name="message">Human-readable warning message.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="code"/>, <paramref name="rootPath"/>, or <paramref name="message"/> is null, empty, or whitespace.
	/// </exception>
	public ContainerVolumeDiscoveryWarning(string code, string rootPath, string message)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(code);
		ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(message);

		Code = code;
		RootPath = rootPath;
		Message = message;
	}

	/// <summary>
	/// Gets the stable warning code.
	/// </summary>
	public string Code
	{
		get;
	}

	/// <summary>
	/// Gets the root path associated with the warning.
	/// </summary>
	public string RootPath
	{
		get;
	}

	/// <summary>
	/// Gets the human-readable warning message.
	/// </summary>
	public string Message
	{
		get;
	}
}
