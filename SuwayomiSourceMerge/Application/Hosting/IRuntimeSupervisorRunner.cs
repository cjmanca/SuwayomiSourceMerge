using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Application.Hosting;

/// <summary>
/// Builds and runs runtime daemon supervision from bootstrapped configuration.
/// </summary>
internal interface IRuntimeSupervisorRunner
{
	/// <summary>
	/// Runs runtime supervision using the provided configuration documents and logger.
	/// </summary>
	/// <param name="documents">Bootstrapped configuration document set.</param>
	/// <param name="logger">Runtime logger instance.</param>
	/// <returns>Zero for success; non-zero for fatal runtime failures.</returns>
	int Run(ConfigurationDocumentSet documents, ISsmLogger logger);
}
