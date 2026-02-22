namespace SuwayomiSourceMerge.UnitTests.Application.Mounting;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="MergeMountWorkflowOptions"/>.
/// </summary>
public sealed class MergeMountWorkflowOptionsTests
{
	/// <summary>
	/// Verifies metadata orchestration options map from default settings values.
	/// </summary>
	[Fact]
	public void FromSettings_Expected_ShouldMapMetadataOrchestrationDefaults()
	{
		SettingsDocument settings = SettingsDocumentDefaults.Create();

		MergeMountWorkflowOptions options = MergeMountWorkflowOptions.FromSettings(settings);

		Assert.Equal(TimeSpan.FromHours(24), options.MetadataOrchestration.ComickMetadataCooldown);
		Assert.Null(options.MetadataOrchestration.FlaresolverrServerUri);
		Assert.Equal(TimeSpan.FromMinutes(60), options.MetadataOrchestration.FlaresolverrDirectRetryInterval);
		Assert.Equal("en", options.MetadataOrchestration.PreferredLanguage);
	}

	/// <summary>
	/// Verifies non-empty FlareSolverr URL values map to absolute URIs.
	/// </summary>
	[Fact]
	public void FromSettings_Edge_ShouldMapAbsoluteFlaresolverrUrl()
	{
		SettingsDocument defaults = SettingsDocumentDefaults.Create();
		SettingsRuntimeSection defaultRuntime = defaults.Runtime!;
		SettingsDocument settings = new()
		{
			Paths = defaults.Paths,
			Scan = defaults.Scan,
			Rename = defaults.Rename,
			Diagnostics = defaults.Diagnostics,
			Shutdown = defaults.Shutdown,
			Permissions = defaults.Permissions,
			Runtime = new SettingsRuntimeSection
			{
				LowPriority = defaultRuntime.LowPriority,
				StartupCleanup = defaultRuntime.StartupCleanup,
				RescanNow = defaultRuntime.RescanNow,
				EnableMountHealthcheck = defaultRuntime.EnableMountHealthcheck,
				MaxConsecutiveMountFailures = defaultRuntime.MaxConsecutiveMountFailures,
				ComickMetadataCooldownHours = defaultRuntime.ComickMetadataCooldownHours,
				FlaresolverrServerUrl = "https://flaresolverr.example.local/",
				FlaresolverrDirectRetryMinutes = defaultRuntime.FlaresolverrDirectRetryMinutes,
				PreferredLanguage = defaultRuntime.PreferredLanguage,
				DetailsDescriptionMode = defaultRuntime.DetailsDescriptionMode,
				MergerfsOptionsBase = defaultRuntime.MergerfsOptionsBase,
				ExcludedSources = defaultRuntime.ExcludedSources
			},
			Logging = defaults.Logging
		};

		MergeMountWorkflowOptions options = MergeMountWorkflowOptions.FromSettings(settings);

		Assert.NotNull(options.MetadataOrchestration.FlaresolverrServerUri);
		Assert.Equal("https://flaresolverr.example.local/", options.MetadataOrchestration.FlaresolverrServerUri!.AbsoluteUri);
	}

	/// <summary>
	/// Verifies missing required metadata fields throw deterministic argument failures.
	/// </summary>
	[Fact]
	public void FromSettings_Failure_ShouldThrow_WhenRequiredMetadataFieldsMissing()
	{
		SettingsDocument defaults = SettingsDocumentDefaults.Create();
		SettingsRuntimeSection defaultRuntime = defaults.Runtime!;
		SettingsDocument settings = new()
		{
			Paths = defaults.Paths,
			Scan = defaults.Scan,
			Rename = defaults.Rename,
			Diagnostics = defaults.Diagnostics,
			Shutdown = defaults.Shutdown,
			Permissions = defaults.Permissions,
			Runtime = new SettingsRuntimeSection
			{
				LowPriority = defaultRuntime.LowPriority,
				StartupCleanup = defaultRuntime.StartupCleanup,
				RescanNow = defaultRuntime.RescanNow,
				EnableMountHealthcheck = defaultRuntime.EnableMountHealthcheck,
				MaxConsecutiveMountFailures = defaultRuntime.MaxConsecutiveMountFailures,
				DetailsDescriptionMode = defaultRuntime.DetailsDescriptionMode,
				MergerfsOptionsBase = defaultRuntime.MergerfsOptionsBase,
				ExcludedSources = defaultRuntime.ExcludedSources
			},
			Logging = defaults.Logging
		};

		Assert.Throws<ArgumentException>(() => MergeMountWorkflowOptions.FromSettings(settings));
	}

	/// <summary>
	/// Verifies invalid metadata values throw option-layer guard exceptions when validation is bypassed.
	/// </summary>
	[Fact]
	public void FromSettings_Failure_ShouldThrow_WhenMetadataValuesInvalid()
	{
		SettingsDocument defaults = SettingsDocumentDefaults.Create();
		SettingsRuntimeSection defaultRuntime = defaults.Runtime!;

		SettingsDocument invalidUriSettings = new()
		{
			Paths = defaults.Paths,
			Scan = defaults.Scan,
			Rename = defaults.Rename,
			Diagnostics = defaults.Diagnostics,
			Shutdown = defaults.Shutdown,
			Permissions = defaults.Permissions,
			Runtime = new SettingsRuntimeSection
			{
				LowPriority = defaultRuntime.LowPriority,
				StartupCleanup = defaultRuntime.StartupCleanup,
				RescanNow = defaultRuntime.RescanNow,
				EnableMountHealthcheck = defaultRuntime.EnableMountHealthcheck,
				MaxConsecutiveMountFailures = defaultRuntime.MaxConsecutiveMountFailures,
				ComickMetadataCooldownHours = defaultRuntime.ComickMetadataCooldownHours,
				FlaresolverrServerUrl = "not-a-uri",
				FlaresolverrDirectRetryMinutes = defaultRuntime.FlaresolverrDirectRetryMinutes,
				PreferredLanguage = defaultRuntime.PreferredLanguage,
				DetailsDescriptionMode = defaultRuntime.DetailsDescriptionMode,
				MergerfsOptionsBase = defaultRuntime.MergerfsOptionsBase,
				ExcludedSources = defaultRuntime.ExcludedSources
			},
			Logging = defaults.Logging
		};

		SettingsDocument invalidCooldownSettings = new()
		{
			Paths = defaults.Paths,
			Scan = defaults.Scan,
			Rename = defaults.Rename,
			Diagnostics = defaults.Diagnostics,
			Shutdown = defaults.Shutdown,
			Permissions = defaults.Permissions,
			Runtime = new SettingsRuntimeSection
			{
				LowPriority = defaultRuntime.LowPriority,
				StartupCleanup = defaultRuntime.StartupCleanup,
				RescanNow = defaultRuntime.RescanNow,
				EnableMountHealthcheck = defaultRuntime.EnableMountHealthcheck,
				MaxConsecutiveMountFailures = defaultRuntime.MaxConsecutiveMountFailures,
				ComickMetadataCooldownHours = 0,
				FlaresolverrServerUrl = string.Empty,
				FlaresolverrDirectRetryMinutes = defaultRuntime.FlaresolverrDirectRetryMinutes,
				PreferredLanguage = defaultRuntime.PreferredLanguage,
				DetailsDescriptionMode = defaultRuntime.DetailsDescriptionMode,
				MergerfsOptionsBase = defaultRuntime.MergerfsOptionsBase,
				ExcludedSources = defaultRuntime.ExcludedSources
			},
			Logging = defaults.Logging
		};

		SettingsDocument invalidSchemeSettings = new()
		{
			Paths = defaults.Paths,
			Scan = defaults.Scan,
			Rename = defaults.Rename,
			Diagnostics = defaults.Diagnostics,
			Shutdown = defaults.Shutdown,
			Permissions = defaults.Permissions,
			Runtime = new SettingsRuntimeSection
			{
				LowPriority = defaultRuntime.LowPriority,
				StartupCleanup = defaultRuntime.StartupCleanup,
				RescanNow = defaultRuntime.RescanNow,
				EnableMountHealthcheck = defaultRuntime.EnableMountHealthcheck,
				MaxConsecutiveMountFailures = defaultRuntime.MaxConsecutiveMountFailures,
				ComickMetadataCooldownHours = defaultRuntime.ComickMetadataCooldownHours,
				FlaresolverrServerUrl = "ftp://flaresolverr.example.local/",
				FlaresolverrDirectRetryMinutes = defaultRuntime.FlaresolverrDirectRetryMinutes,
				PreferredLanguage = defaultRuntime.PreferredLanguage,
				DetailsDescriptionMode = defaultRuntime.DetailsDescriptionMode,
				MergerfsOptionsBase = defaultRuntime.MergerfsOptionsBase,
				ExcludedSources = defaultRuntime.ExcludedSources
			},
			Logging = defaults.Logging
		};

		SettingsDocument invalidLanguageSettings = new()
		{
			Paths = defaults.Paths,
			Scan = defaults.Scan,
			Rename = defaults.Rename,
			Diagnostics = defaults.Diagnostics,
			Shutdown = defaults.Shutdown,
			Permissions = defaults.Permissions,
			Runtime = new SettingsRuntimeSection
			{
				LowPriority = defaultRuntime.LowPriority,
				StartupCleanup = defaultRuntime.StartupCleanup,
				RescanNow = defaultRuntime.RescanNow,
				EnableMountHealthcheck = defaultRuntime.EnableMountHealthcheck,
				MaxConsecutiveMountFailures = defaultRuntime.MaxConsecutiveMountFailures,
				ComickMetadataCooldownHours = defaultRuntime.ComickMetadataCooldownHours,
				FlaresolverrServerUrl = string.Empty,
				FlaresolverrDirectRetryMinutes = defaultRuntime.FlaresolverrDirectRetryMinutes,
				PreferredLanguage = "   ",
				DetailsDescriptionMode = defaultRuntime.DetailsDescriptionMode,
				MergerfsOptionsBase = defaultRuntime.MergerfsOptionsBase,
				ExcludedSources = defaultRuntime.ExcludedSources
			},
			Logging = defaults.Logging
		};

		Assert.Throws<ArgumentException>(() => MergeMountWorkflowOptions.FromSettings(invalidUriSettings));
		Assert.Throws<ArgumentOutOfRangeException>(() => MergeMountWorkflowOptions.FromSettings(invalidCooldownSettings));
		Assert.Throws<ArgumentException>(() => MergeMountWorkflowOptions.FromSettings(invalidSchemeSettings));
		Assert.Throws<ArgumentException>(() => MergeMountWorkflowOptions.FromSettings(invalidLanguageSettings));
	}
}

