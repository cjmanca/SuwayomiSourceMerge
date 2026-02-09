using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

internal sealed class SettingsSelfHealingResult
{
    public SettingsSelfHealingResult(SettingsDocument document, bool wasHealed)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        WasHealed = wasHealed;
    }

    public SettingsDocument Document
    {
        get;
    }

    public bool WasHealed
    {
        get;
    }
}
