namespace SuwayomiSourceMerge.UnitTests.Configuration;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Validation;

public sealed class SettingsDocumentDefaultsTests
{
    [Fact]
    public void Create_ShouldReturnValidDocumentWithLoggingDefaults()
    {
        SettingsDocument document = SettingsDocumentDefaults.Create();
        SettingsDocumentValidator validator = new();

        ValidationResult validation = validator.Validate(document, "settings.yml");

        Assert.True(validation.IsValid);
        Assert.NotNull(document.Logging);
        Assert.NotNull(document.Shutdown);
        Assert.Equal("daemon.log", document.Logging!.FileName);
        Assert.Equal(10, document.Logging.MaxFileSizeMb);
        Assert.Equal(10, document.Logging.RetainedFileCount);
        Assert.Equal("normal", document.Logging.Level);
        Assert.Null(document.Scan!.MergeTriggerRequestTimeoutBufferSeconds);
        Assert.Equal("progressive", document.Scan.WatchStartupMode);
        Assert.Equal(24, document.Runtime!.ComickMetadataCooldownHours);
        Assert.Equal(string.Empty, document.Runtime.FlaresolverrServerUrl);
        Assert.Equal(60, document.Runtime.FlaresolverrDirectRetryMinutes);
        Assert.Equal("en", document.Runtime.PreferredLanguage);
        Assert.False(document.Shutdown!.CleanupApplyHighPriority);
        Assert.Equal(3, document.Shutdown!.CleanupPriorityIoniceClass);
        Assert.Equal(-20, document.Shutdown.CleanupPriorityNiceValue);
    }

    [Fact]
    public void Create_ShouldReturnIndependentExcludedSourcesListsAcrossCalls()
    {
        SettingsDocument first = SettingsDocumentDefaults.Create();
        SettingsDocument second = SettingsDocumentDefaults.Create();

        first.Runtime!.ExcludedSources!.Add("Injected Source");

        Assert.DoesNotContain("Injected Source", second.Runtime!.ExcludedSources!);
    }

    [Fact]
    public void Create_ShouldReturnNewDocumentInstanceEachCall()
    {
        SettingsDocument first = SettingsDocumentDefaults.Create();
        SettingsDocument second = SettingsDocumentDefaults.Create();

        Assert.NotSame(first, second);
        Assert.NotSame(first.Paths, second.Paths);
        Assert.NotSame(first.Logging, second.Logging);
    }
}
