namespace SuwayomiSourceMerge.UnitTests.Configuration.Validation;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Validation;
using SuwayomiSourceMerge.UnitTests.Configuration;

/// <summary>
/// Runtime mount-failure threshold validation coverage for <see cref="SettingsDocumentValidator"/>.
/// </summary>
public sealed partial class SettingsDocumentValidatorTests
{
	/// <summary>
	/// Verifies positive runtime mount-failure thresholds are accepted.
	/// </summary>
	[Fact]
	public void Validate_RuntimeMountFailureThreshold_Expected_ShouldAcceptPositiveValue()
	{
		SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
		SettingsDocument document = CloneWithRuntimeThreshold(baseline, 5);
		SettingsDocumentValidator validator = new();

		ValidationResult result = validator.Validate(document, "settings.yml");

		Assert.True(result.IsValid);
	}

	/// <summary>
	/// Verifies the minimum positive runtime mount-failure threshold is accepted.
	/// </summary>
	[Fact]
	public void Validate_RuntimeMountFailureThreshold_Edge_ShouldAcceptOne()
	{
		SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
		SettingsDocument document = CloneWithRuntimeThreshold(baseline, 1);
		SettingsDocumentValidator validator = new();

		ValidationResult result = validator.Validate(document, "settings.yml");

		Assert.True(result.IsValid);
	}

	/// <summary>
	/// Verifies non-positive runtime mount-failure thresholds are rejected.
	/// </summary>
	[Fact]
	public void Validate_RuntimeMountFailureThreshold_Failure_ShouldRejectZero()
	{
		SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
		SettingsDocument document = CloneWithRuntimeThreshold(baseline, 0);
		SettingsDocumentValidator validator = new();

		ValidationResult result = validator.Validate(document, "settings.yml");

		ValidationError error = Assert.Single(result.Errors);
		Assert.Equal("$.runtime.max_consecutive_mount_failures", error.Path);
		Assert.Equal("CFG-SET-004", error.Code);
	}

	private static SettingsDocument CloneWithRuntimeThreshold(SettingsDocument baseline, int threshold)
	{
		return new SettingsDocument
		{
			Paths = baseline.Paths,
			Scan = baseline.Scan,
			Rename = baseline.Rename,
			Diagnostics = baseline.Diagnostics,
			Shutdown = baseline.Shutdown,
			Permissions = baseline.Permissions,
			Runtime = new SettingsRuntimeSection
			{
				LowPriority = baseline.Runtime!.LowPriority,
				StartupCleanup = baseline.Runtime.StartupCleanup,
				RescanNow = baseline.Runtime.RescanNow,
				EnableMountHealthcheck = baseline.Runtime.EnableMountHealthcheck,
				MaxConsecutiveMountFailures = threshold,
				ComickMetadataCooldownHours = baseline.Runtime.ComickMetadataCooldownHours,
				FlaresolverrServerUrl = baseline.Runtime.FlaresolverrServerUrl,
				FlaresolverrDirectRetryMinutes = baseline.Runtime.FlaresolverrDirectRetryMinutes,
				PreferredLanguage = baseline.Runtime.PreferredLanguage,
				DetailsDescriptionMode = baseline.Runtime.DetailsDescriptionMode,
				MergerfsOptionsBase = baseline.Runtime.MergerfsOptionsBase,
				ExcludedSources = baseline.Runtime.ExcludedSources
			},
			Logging = baseline.Logging
		};
	}
}
