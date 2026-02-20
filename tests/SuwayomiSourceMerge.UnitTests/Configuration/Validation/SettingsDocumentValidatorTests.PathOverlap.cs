namespace SuwayomiSourceMerge.UnitTests.Configuration.Validation;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Validation;
using SuwayomiSourceMerge.UnitTests.Configuration;

/// <summary>
/// Config and merged path-overlap validation coverage for <see cref="SettingsDocumentValidator"/>.
/// </summary>
public sealed partial class SettingsDocumentValidatorTests
{
	/// <summary>
	/// Verifies equal config and merged paths are rejected.
	/// </summary>
	[Fact]
	public void Validate_PathOverlap_Failure_ShouldRejectEqualConfigAndMergedPaths()
	{
		SettingsDocumentValidator validator = new();
		SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
		SettingsDocument document = CloneWithPaths(
			baseline,
			configRootPath: "/ssm/shared",
			mergedRootPath: "/ssm/shared");

		ValidationResult result = validator.Validate(document, "settings.yml");

		Assert.Contains(result.Errors, static error => error.Path == "$.paths.config_root_path" && error.Code == "CFG-SET-008");
		Assert.Contains(result.Errors, static error => error.Path == "$.paths.merged_root_path" && error.Code == "CFG-SET-008");
	}

	/// <summary>
	/// Verifies config paths nested under merged paths are rejected.
	/// </summary>
	[Fact]
	public void Validate_PathOverlap_Failure_ShouldRejectConfigPathInsideMergedPath()
	{
		SettingsDocumentValidator validator = new();
		SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
		SettingsDocument document = CloneWithPaths(
			baseline,
			configRootPath: "/ssm/merged/config",
			mergedRootPath: "/ssm/merged");

		ValidationResult result = validator.Validate(document, "settings.yml");

		Assert.Contains(result.Errors, static error => error.Path == "$.paths.config_root_path" && error.Code == "CFG-SET-008");
		Assert.Contains(result.Errors, static error => error.Path == "$.paths.merged_root_path" && error.Code == "CFG-SET-008");
	}

	/// <summary>
	/// Verifies merged paths nested under config paths are rejected.
	/// </summary>
	[Fact]
	public void Validate_PathOverlap_Failure_ShouldRejectMergedPathInsideConfigPath()
	{
		SettingsDocumentValidator validator = new();
		SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
		SettingsDocument document = CloneWithPaths(
			baseline,
			configRootPath: "/ssm/config",
			mergedRootPath: "/ssm/config/merged");

		ValidationResult result = validator.Validate(document, "settings.yml");

		Assert.Contains(result.Errors, static error => error.Path == "$.paths.config_root_path" && error.Code == "CFG-SET-008");
		Assert.Contains(result.Errors, static error => error.Path == "$.paths.merged_root_path" && error.Code == "CFG-SET-008");
	}

	/// <summary>
	/// Verifies overlap detection still rejects nested paths when config has a trailing directory separator.
	/// </summary>
	[Fact]
	public void Validate_PathOverlap_Edge_ShouldRejectMergedPathInsideConfigPath_WhenConfigHasTrailingSeparator()
	{
		SettingsDocumentValidator validator = new();
		SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
		SettingsDocument document = CloneWithPaths(
			baseline,
			configRootPath: "/ssm/config/",
			mergedRootPath: "/ssm/config/merged");

		ValidationResult result = validator.Validate(document, "settings.yml");

		Assert.Contains(result.Errors, static error => error.Path == "$.paths.config_root_path" && error.Code == "CFG-SET-008");
		Assert.Contains(result.Errors, static error => error.Path == "$.paths.merged_root_path" && error.Code == "CFG-SET-008");
	}

	/// <summary>
	/// Verifies non-overlapping sibling config and merged paths are accepted.
	/// </summary>
	[Fact]
	public void Validate_PathOverlap_Expected_ShouldAllowSiblingPaths()
	{
		SettingsDocumentValidator validator = new();
		SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
		SettingsDocument document = CloneWithPaths(
			baseline,
			configRootPath: "/ssm/config",
			mergedRootPath: "/ssm/merged");

		ValidationResult result = validator.Validate(document, "settings.yml");

		Assert.DoesNotContain(result.Errors, static error => error.Code == "CFG-SET-008");
		Assert.True(result.IsValid);
	}

	/// <summary>
	/// Clones one settings document and applies path overrides.
	/// </summary>
	/// <param name="baseline">Baseline document.</param>
	/// <param name="configRootPath">Config root path override.</param>
	/// <param name="mergedRootPath">Merged root path override.</param>
	/// <returns>Cloned settings document with path overrides.</returns>
	private static SettingsDocument CloneWithPaths(
		SettingsDocument baseline,
		string configRootPath,
		string mergedRootPath)
	{
		return new SettingsDocument
		{
			Paths = new SettingsPathsSection
			{
				ConfigRootPath = configRootPath,
				SourcesRootPath = baseline.Paths!.SourcesRootPath,
				OverrideRootPath = baseline.Paths.OverrideRootPath,
				MergedRootPath = mergedRootPath,
				StateRootPath = baseline.Paths.StateRootPath,
				LogRootPath = baseline.Paths.LogRootPath,
				BranchLinksRootPath = baseline.Paths.BranchLinksRootPath,
				UnraidCachePoolName = baseline.Paths.UnraidCachePoolName
			},
			Scan = baseline.Scan,
			Rename = baseline.Rename,
			Diagnostics = baseline.Diagnostics,
			Shutdown = baseline.Shutdown,
			Permissions = baseline.Permissions,
			Runtime = baseline.Runtime,
			Logging = baseline.Logging
		};
	}
}
