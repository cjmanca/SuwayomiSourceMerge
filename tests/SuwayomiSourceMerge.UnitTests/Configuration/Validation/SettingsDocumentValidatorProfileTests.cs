namespace SuwayomiSourceMerge.UnitTests.Configuration.Validation;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Validation;
using SuwayomiSourceMerge.UnitTests.Configuration;

/// <summary>
/// Verifies strict-runtime versus relaxed-tooling profile behavior for <see cref="SettingsDocumentValidator"/>.
/// </summary>
public sealed class SettingsDocumentValidatorProfileTests
{
    [Fact]
    public void Validate_Failure_ShouldRequireShutdownCleanupProfileFields_WhenProfileIsStrictRuntime()
    {
        SettingsDocumentValidator validator = new(SettingsValidationProfile.StrictRuntime);
        SettingsDocument document = CreateDocumentWithShutdown(
            new SettingsShutdownSection
            {
                UnmountOnExit = true,
                StopTimeoutSeconds = 120,
                ChildExitGraceSeconds = 5,
                UnmountCommandTimeoutSeconds = 8,
                UnmountDetachWaitSeconds = 5,
                CleanupHighPriority = true,
                CleanupApplyHighPriority = null,
                CleanupPriorityIoniceClass = null,
                CleanupPriorityNiceValue = null
            });

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.shutdown.cleanup_apply_high_priority" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.shutdown.cleanup_priority_ionice_class" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.shutdown.cleanup_priority_nice_value" && error.Code == "CFG-SET-002");
    }

    [Fact]
    public void Validate_Expected_ShouldAllowMissingShutdownCleanupProfileFields_WhenProfileIsRelaxedTooling()
    {
        SettingsDocumentValidator validator = new(SettingsValidationProfile.RelaxedTooling);
        SettingsDocument document = CreateDocumentWithShutdown(
            new SettingsShutdownSection
            {
                UnmountOnExit = true,
                StopTimeoutSeconds = 120,
                ChildExitGraceSeconds = 5,
                UnmountCommandTimeoutSeconds = 8,
                UnmountDetachWaitSeconds = 5,
                CleanupHighPriority = true,
                CleanupApplyHighPriority = null,
                CleanupPriorityIoniceClass = null,
                CleanupPriorityNiceValue = null
            });

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Failure_ShouldStillValidateNumericRanges_WhenProfileIsRelaxedTooling()
    {
        SettingsDocumentValidator validator = new(SettingsValidationProfile.RelaxedTooling);
        SettingsDocument document = CreateDocumentWithShutdown(
            new SettingsShutdownSection
            {
                UnmountOnExit = true,
                StopTimeoutSeconds = 120,
                ChildExitGraceSeconds = 5,
                UnmountCommandTimeoutSeconds = 8,
                UnmountDetachWaitSeconds = 5,
                CleanupHighPriority = true,
                CleanupApplyHighPriority = null,
                CleanupPriorityIoniceClass = 9,
                CleanupPriorityNiceValue = 25
            });

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.shutdown.cleanup_priority_ionice_class" && error.Code == "CFG-SET-004");
        Assert.Contains(result.Errors, error => error.Path == "$.shutdown.cleanup_priority_nice_value" && error.Code == "CFG-SET-004");
        Assert.DoesNotContain(result.Errors, error => error.Path == "$.shutdown.cleanup_apply_high_priority");
    }

    private static SettingsDocument CreateDocumentWithShutdown(SettingsShutdownSection shutdown)
    {
        SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
        return new SettingsDocument
        {
            Paths = baseline.Paths,
            Scan = baseline.Scan,
            Rename = baseline.Rename,
            Diagnostics = baseline.Diagnostics,
            Shutdown = shutdown,
            Permissions = baseline.Permissions,
            Runtime = baseline.Runtime,
            Logging = baseline.Logging
        };
    }
}
