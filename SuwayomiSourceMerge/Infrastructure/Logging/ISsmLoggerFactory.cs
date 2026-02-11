using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Creates <see cref="ISsmLogger"/> instances from validated runtime settings.
/// </summary>
internal interface ISsmLoggerFactory
{
	/// <summary>
	/// Builds a logger configured from the provided settings document.
	/// </summary>
	/// <param name="settings">Validated settings document that supplies log path, level, and retention values.</param>
	/// <param name="fallbackErrorWriter">
	/// Callback used when sink-level logging failures occur and an out-of-band message should be written.
	/// </param>
	/// <returns>A configured logger ready for runtime use.</returns>
	ISsmLogger Create(SettingsDocument settings, Action<string> fallbackErrorWriter);
}
