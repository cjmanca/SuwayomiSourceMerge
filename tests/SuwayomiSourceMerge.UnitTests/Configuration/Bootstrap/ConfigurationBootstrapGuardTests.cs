namespace SuwayomiSourceMerge.UnitTests.Configuration.Bootstrap;

using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Tests constructor and argument guard behavior for bootstrap-related types.
/// </summary>
public sealed class ConfigurationBootstrapGuardTests
{
    [Fact]
    public void PublicConstructor_ShouldCreateInstance_WhenSchemaServiceProvided()
    {
        ConfigurationBootstrapService service = new(ConfigurationSchemaServiceFactory.CreateSchemaService());

        Assert.NotNull(service);
    }

    [Fact]
    public void PublicConstructor_ShouldThrow_WhenSchemaServiceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ConfigurationBootstrapService(null!));
    }

    [Fact]
    public void InternalConstructor_ShouldCreateInstance_WhenDependenciesProvided()
    {
        ConfigurationBootstrapService service = new(
            ConfigurationSchemaServiceFactory.CreateSchemaService(),
            new LegacyMangaEquivalentsMigrator(),
            new LegacySourcePriorityMigrator(),
            new SettingsSelfHealingService(new YamlDocumentParser()),
            new YamlDocumentWriter());

        Assert.NotNull(service);
    }

    [Fact]
    public void InternalConstructor_ShouldThrow_WhenAnyDependencyIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ConfigurationBootstrapService(
                null!,
                new LegacyMangaEquivalentsMigrator(),
                new LegacySourcePriorityMigrator(),
                new SettingsSelfHealingService(new YamlDocumentParser()),
                new YamlDocumentWriter()));

        Assert.Throws<ArgumentNullException>(
            () => new ConfigurationBootstrapService(
                ConfigurationSchemaServiceFactory.CreateSchemaService(),
                null!,
                new LegacySourcePriorityMigrator(),
                new SettingsSelfHealingService(new YamlDocumentParser()),
                new YamlDocumentWriter()));

        Assert.Throws<ArgumentNullException>(
            () => new ConfigurationBootstrapService(
                ConfigurationSchemaServiceFactory.CreateSchemaService(),
                new LegacyMangaEquivalentsMigrator(),
                null!,
                new SettingsSelfHealingService(new YamlDocumentParser()),
                new YamlDocumentWriter()));

        Assert.Throws<ArgumentNullException>(
            () => new ConfigurationBootstrapService(
                ConfigurationSchemaServiceFactory.CreateSchemaService(),
                new LegacyMangaEquivalentsMigrator(),
                new LegacySourcePriorityMigrator(),
                null!,
                new YamlDocumentWriter()));

        Assert.Throws<ArgumentNullException>(
            () => new ConfigurationBootstrapService(
                ConfigurationSchemaServiceFactory.CreateSchemaService(),
                new LegacyMangaEquivalentsMigrator(),
                new LegacySourcePriorityMigrator(),
                new SettingsSelfHealingService(new YamlDocumentParser()),
                null!));
    }

    [Fact]
    public void Bootstrap_ShouldThrow_WhenConfigRootPathIsWhitespace()
    {
        ConfigurationBootstrapService service = new(ConfigurationSchemaServiceFactory.CreateSchemaService());

        Assert.Throws<ArgumentException>(() => service.Bootstrap("   "));
    }

    [Fact]
    public void ConfigurationBootstrapFileState_ShouldStoreValues_WhenValid()
    {
        ConfigurationBootstrapFileState state = new(
            "settings.yml",
            "/ssm/config/settings.yml",
            wasCreated: false,
            wasMigrated: false,
            usedDefaults: true,
            wasSelfHealed: true);

        Assert.Equal("settings.yml", state.FileName);
        Assert.Equal("/ssm/config/settings.yml", state.FilePath);
        Assert.True(state.UsedDefaults);
        Assert.True(state.WasSelfHealed);
    }

    [Fact]
    public void ConfigurationBootstrapFileState_ShouldAllowBooleanEdgeCombination()
    {
        ConfigurationBootstrapFileState state = new(
            "scene_tags.yml",
            "/ssm/config/scene_tags.yml",
            wasCreated: false,
            wasMigrated: false,
            usedDefaults: false,
            wasSelfHealed: false);

        Assert.False(state.WasCreated);
        Assert.False(state.WasMigrated);
        Assert.False(state.UsedDefaults);
        Assert.False(state.WasSelfHealed);
    }

    [Fact]
    public void ConfigurationBootstrapFileState_ShouldThrow_WhenNameOrPathInvalid()
    {
        Assert.Throws<ArgumentException>(
            () => new ConfigurationBootstrapFileState(
                "",
                "/ssm/config/settings.yml",
                wasCreated: false,
                wasMigrated: false,
                usedDefaults: false,
                wasSelfHealed: false));

        Assert.Throws<ArgumentException>(
            () => new ConfigurationBootstrapFileState(
                "settings.yml",
                " ",
                wasCreated: false,
                wasMigrated: false,
                usedDefaults: false,
                wasSelfHealed: false));
    }

    [Fact]
    public void SettingsSelfHealingResult_ShouldStoreValues_WhenDocumentProvided()
    {
        SettingsSelfHealingResult result = new(new SettingsDocument(), wasHealed: false);

        Assert.NotNull(result.Document);
        Assert.False(result.WasHealed);
    }

    [Fact]
    public void SettingsSelfHealingResult_ShouldSupportWasHealedTrue()
    {
        SettingsSelfHealingResult result = new(new SettingsDocument(), wasHealed: true);

        Assert.True(result.WasHealed);
    }

    [Fact]
    public void SettingsSelfHealingResult_ShouldThrow_WhenDocumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsSelfHealingResult(null!, wasHealed: false));
    }
}
