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

    [Fact]
    public void Validate_Failure_ShouldRequireComickRuntimeFields_WhenProfileIsStrictRuntime()
    {
        SettingsDocumentValidator validator = new(SettingsValidationProfile.StrictRuntime);
        SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
        SettingsDocument document = CreateDocumentWithRuntime(
            baseline,
            new SettingsRuntimeSection
            {
                LowPriority = baseline.Runtime!.LowPriority,
                StartupCleanup = baseline.Runtime.StartupCleanup,
                RescanNow = baseline.Runtime.RescanNow,
                EnableMountHealthcheck = baseline.Runtime.EnableMountHealthcheck,
                MaxConsecutiveMountFailures = baseline.Runtime.MaxConsecutiveMountFailures,
                ComickMetadataCooldownHours = null,
                MetadataApiRequestDelayMs = null,
                MetadataApiCacheTtlHours = null,
                FlaresolverrServerUrl = null,
                FlaresolverrDirectRetryMinutes = null,
                PreferredLanguage = null,
                DetailsDescriptionMode = baseline.Runtime.DetailsDescriptionMode,
                MergerfsOptionsBase = baseline.Runtime.MergerfsOptionsBase,
                ExcludedSources = baseline.Runtime.ExcludedSources
            });

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.runtime.comick_metadata_cooldown_hours" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.metadata_api_request_delay_ms" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.metadata_api_cache_ttl_hours" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.flaresolverr_server_url" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.flaresolverr_direct_retry_minutes" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.preferred_language" && error.Code == "CFG-SET-002");
    }

    [Fact]
    public void Validate_Expected_ShouldAcceptValidComickRuntimeFields_WhenProfileIsStrictRuntime()
    {
        SettingsDocumentValidator validator = new(SettingsValidationProfile.StrictRuntime);
        SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
        SettingsDocument document = CreateDocumentWithRuntime(
            baseline,
            new SettingsRuntimeSection
            {
                LowPriority = baseline.Runtime!.LowPriority,
                StartupCleanup = baseline.Runtime.StartupCleanup,
                RescanNow = baseline.Runtime.RescanNow,
                EnableMountHealthcheck = baseline.Runtime.EnableMountHealthcheck,
                MaxConsecutiveMountFailures = baseline.Runtime.MaxConsecutiveMountFailures,
                ComickMetadataCooldownHours = 24,
                MetadataApiRequestDelayMs = 0,
                MetadataApiCacheTtlHours = 24,
                FlaresolverrServerUrl = "https://flaresolverr.example.local/",
                FlaresolverrDirectRetryMinutes = 60,
                PreferredLanguage = "zh-CN",
                DetailsDescriptionMode = baseline.Runtime.DetailsDescriptionMode,
                MergerfsOptionsBase = baseline.Runtime.MergerfsOptionsBase,
                ExcludedSources = baseline.Runtime.ExcludedSources
            });

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Expected_ShouldAllowMissingComickRuntimeFields_WhenProfileIsRelaxedTooling()
    {
        SettingsDocumentValidator validator = new(SettingsValidationProfile.RelaxedTooling);
        SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
        SettingsDocument document = CreateDocumentWithRuntime(
            baseline,
            new SettingsRuntimeSection
            {
                LowPriority = baseline.Runtime!.LowPriority,
                StartupCleanup = baseline.Runtime.StartupCleanup,
                RescanNow = baseline.Runtime.RescanNow,
                EnableMountHealthcheck = baseline.Runtime.EnableMountHealthcheck,
                MaxConsecutiveMountFailures = baseline.Runtime.MaxConsecutiveMountFailures,
                ComickMetadataCooldownHours = null,
                MetadataApiRequestDelayMs = null,
                MetadataApiCacheTtlHours = null,
                FlaresolverrServerUrl = null,
                FlaresolverrDirectRetryMinutes = null,
                PreferredLanguage = null,
                DetailsDescriptionMode = baseline.Runtime.DetailsDescriptionMode,
                MergerfsOptionsBase = baseline.Runtime.MergerfsOptionsBase,
                ExcludedSources = baseline.Runtime.ExcludedSources
            });

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Failure_ShouldValidateOptionalComickRuntimeFields_WhenProfileIsRelaxedTooling()
    {
        SettingsDocumentValidator validator = new(SettingsValidationProfile.RelaxedTooling);
        SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
        SettingsDocument document = CreateDocumentWithRuntime(
            baseline,
            new SettingsRuntimeSection
            {
                LowPriority = baseline.Runtime!.LowPriority,
                StartupCleanup = baseline.Runtime.StartupCleanup,
                RescanNow = baseline.Runtime.RescanNow,
                EnableMountHealthcheck = baseline.Runtime.EnableMountHealthcheck,
                MaxConsecutiveMountFailures = baseline.Runtime.MaxConsecutiveMountFailures,
                ComickMetadataCooldownHours = 0,
                MetadataApiRequestDelayMs = -1,
                MetadataApiCacheTtlHours = 0,
                FlaresolverrServerUrl = "ftp://flaresolverr.example.local",
                FlaresolverrDirectRetryMinutes = 0,
                PreferredLanguage = " ",
                DetailsDescriptionMode = baseline.Runtime.DetailsDescriptionMode,
                MergerfsOptionsBase = baseline.Runtime.MergerfsOptionsBase,
                ExcludedSources = baseline.Runtime.ExcludedSources
            });

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.runtime.comick_metadata_cooldown_hours" && error.Code == "CFG-SET-004");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.metadata_api_request_delay_ms" && error.Code == "CFG-SET-004");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.metadata_api_cache_ttl_hours" && error.Code == "CFG-SET-004");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.flaresolverr_server_url" && error.Code == "CFG-SET-005");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.flaresolverr_direct_retry_minutes" && error.Code == "CFG-SET-004");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.preferred_language" && error.Code == "CFG-SET-002");
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

    private static SettingsDocument CreateDocumentWithRuntime(SettingsDocument baseline, SettingsRuntimeSection runtime)
    {
        return new SettingsDocument
        {
            Paths = baseline.Paths,
            Scan = baseline.Scan,
            Rename = baseline.Rename,
            Diagnostics = baseline.Diagnostics,
            Shutdown = baseline.Shutdown,
            Permissions = baseline.Permissions,
            Runtime = runtime,
            Logging = baseline.Logging
        };
    }
}
