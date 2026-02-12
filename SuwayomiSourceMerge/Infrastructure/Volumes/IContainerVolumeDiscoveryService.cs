namespace SuwayomiSourceMerge.Infrastructure.Volumes;

/// <summary>
/// Defines the contract for discovering mapped source and override volumes in a container layout.
/// </summary>
internal interface IContainerVolumeDiscoveryService
{
	/// <summary>
	/// Discovers direct-child source and override volume directories under the provided root paths.
	/// </summary>
	/// <param name="sourcesRootPath">
	/// Absolute or relative path to the root directory whose direct children represent source volumes.
	/// </param>
	/// <param name="overrideRootPath">
	/// Absolute or relative path to the root directory whose direct children represent override volumes.
	/// </param>
	/// <returns>
	/// A discovery result containing normalized source volume paths, override volume paths, and any non-fatal warnings.
	/// </returns>
	ContainerVolumeDiscoveryResult Discover(string sourcesRootPath, string overrideRootPath);
}
