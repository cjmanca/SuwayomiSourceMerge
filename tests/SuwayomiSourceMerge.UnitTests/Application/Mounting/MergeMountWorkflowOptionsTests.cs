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
		Assert.Equal(TimeSpan.FromMilliseconds(1000), options.MetadataOrchestration.MetadataApiRequestDelay);
		Assert.Equal(TimeSpan.FromHours(24), options.MetadataOrchestration.MetadataApiCacheTtl);
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
				MetadataApiRequestDelayMs = defaultRuntime.MetadataApiRequestDelayMs,
				MetadataApiCacheTtlHours = defaultRuntime.MetadataApiCacheTtlHours,
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
				ComickMetadataCooldownHours = defaultRuntime.ComickMetadataCooldownHours,
				FlaresolverrServerUrl = string.Empty,
				FlaresolverrDirectRetryMinutes = defaultRuntime.FlaresolverrDirectRetryMinutes,
				PreferredLanguage = defaultRuntime.PreferredLanguage,
				DetailsDescriptionMode = defaultRuntime.DetailsDescriptionMode,
				MergerfsOptionsBase = defaultRuntime.MergerfsOptionsBase,
				ExcludedSources = defaultRuntime.ExcludedSources
			},
			Logging = defaults.Logging
		};

		ArgumentException exception = Assert.Throws<ArgumentException>(() => MergeMountWorkflowOptions.FromSettings(settings));
		Assert.Equal("settings", exception.ParamName);
		Assert.Contains("runtime.metadata_api_request_delay_ms", exception.Message, StringComparison.Ordinal);
		Assert.Contains("runtime.metadata_api_cache_ttl_hours", exception.Message, StringComparison.Ordinal);
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
				MetadataApiRequestDelayMs = defaultRuntime.MetadataApiRequestDelayMs,
				MetadataApiCacheTtlHours = defaultRuntime.MetadataApiCacheTtlHours,
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
				MetadataApiRequestDelayMs = defaultRuntime.MetadataApiRequestDelayMs,
				MetadataApiCacheTtlHours = defaultRuntime.MetadataApiCacheTtlHours,
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
				MetadataApiRequestDelayMs = defaultRuntime.MetadataApiRequestDelayMs,
				MetadataApiCacheTtlHours = defaultRuntime.MetadataApiCacheTtlHours,
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
				MetadataApiRequestDelayMs = defaultRuntime.MetadataApiRequestDelayMs,
				MetadataApiCacheTtlHours = defaultRuntime.MetadataApiCacheTtlHours,
				FlaresolverrServerUrl = string.Empty,
				FlaresolverrDirectRetryMinutes = defaultRuntime.FlaresolverrDirectRetryMinutes,
				PreferredLanguage = "   ",
				DetailsDescriptionMode = defaultRuntime.DetailsDescriptionMode,
				MergerfsOptionsBase = defaultRuntime.MergerfsOptionsBase,
				ExcludedSources = defaultRuntime.ExcludedSources
			},
			Logging = defaults.Logging
		};

		SettingsDocument invalidRequestDelaySettings = new()
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
				MetadataApiRequestDelayMs = -1,
				MetadataApiCacheTtlHours = defaultRuntime.MetadataApiCacheTtlHours,
				FlaresolverrServerUrl = string.Empty,
				FlaresolverrDirectRetryMinutes = defaultRuntime.FlaresolverrDirectRetryMinutes,
				PreferredLanguage = defaultRuntime.PreferredLanguage,
				DetailsDescriptionMode = defaultRuntime.DetailsDescriptionMode,
				MergerfsOptionsBase = defaultRuntime.MergerfsOptionsBase,
				ExcludedSources = defaultRuntime.ExcludedSources
			},
			Logging = defaults.Logging
		};

		SettingsDocument invalidCacheTtlSettings = new()
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
				MetadataApiRequestDelayMs = defaultRuntime.MetadataApiRequestDelayMs,
				MetadataApiCacheTtlHours = 0,
				FlaresolverrServerUrl = string.Empty,
				FlaresolverrDirectRetryMinutes = defaultRuntime.FlaresolverrDirectRetryMinutes,
				PreferredLanguage = defaultRuntime.PreferredLanguage,
				DetailsDescriptionMode = defaultRuntime.DetailsDescriptionMode,
				MergerfsOptionsBase = defaultRuntime.MergerfsOptionsBase,
				ExcludedSources = defaultRuntime.ExcludedSources
			},
			Logging = defaults.Logging
		};

		ArgumentException invalidUriException = Assert.Throws<ArgumentException>(() => MergeMountWorkflowOptions.FromSettings(invalidUriSettings));
		ArgumentOutOfRangeException invalidCooldownException = Assert.Throws<ArgumentOutOfRangeException>(() => MergeMountWorkflowOptions.FromSettings(invalidCooldownSettings));
		ArgumentException invalidSchemeException = Assert.Throws<ArgumentException>(() => MergeMountWorkflowOptions.FromSettings(invalidSchemeSettings));
		ArgumentException invalidLanguageException = Assert.Throws<ArgumentException>(() => MergeMountWorkflowOptions.FromSettings(invalidLanguageSettings));
		ArgumentOutOfRangeException invalidRequestDelayException = Assert.Throws<ArgumentOutOfRangeException>(() => MergeMountWorkflowOptions.FromSettings(invalidRequestDelaySettings));
		ArgumentOutOfRangeException invalidCacheTtlException = Assert.Throws<ArgumentOutOfRangeException>(() => MergeMountWorkflowOptions.FromSettings(invalidCacheTtlSettings));
		Assert.Equal("settings", invalidUriException.ParamName);
		Assert.Equal("comickMetadataCooldown", invalidCooldownException.ParamName);
		Assert.Equal("settings", invalidSchemeException.ParamName);
		Assert.Equal("preferredLanguage", invalidLanguageException.ParamName);
		Assert.Equal("metadataApiRequestDelay", invalidRequestDelayException.ParamName);
		Assert.Equal("metadataApiCacheTtl", invalidCacheTtlException.ParamName);
	}
}

