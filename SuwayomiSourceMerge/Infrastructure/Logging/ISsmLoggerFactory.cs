using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Infrastructure.Logging;

internal interface ISsmLoggerFactory
{
    ISsmLogger Create(SettingsDocument settings, Action<string> fallbackErrorWriter);
}
