namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Verifies deterministic branch-link naming behavior.
/// </summary>
public sealed class BranchLinkNamingPolicyTests
{
	/// <summary>
	/// Verifies the primary override link name is the expected constant token.
	/// </summary>
	[Fact]
	public void BuildPrimaryOverrideLinkName_Expected_ShouldReturnConstantToken()
	{
		BranchLinkNamingPolicy policy = new();

		string linkName = policy.BuildPrimaryOverrideLinkName();

		Assert.Equal("00_override_primary", linkName);
	}

	/// <summary>
	/// Verifies additional override link names include sanitized volume labels and zero-padded indexes.
	/// </summary>
	[Fact]
	public void BuildAdditionalOverrideLinkName_Expected_ShouldIncludeSanitizedVolumeLabelAndIndex()
	{
		BranchLinkNamingPolicy policy = new();

		string linkName = policy.BuildAdditionalOverrideLinkName("/ssm/override/disk 1", 7);

		Assert.Equal("01_override_disk_1_007", linkName);
	}

	/// <summary>
	/// Verifies source link names include sanitized source labels and zero-padded indexes.
	/// </summary>
	[Fact]
	public void BuildSourceLinkName_Expected_ShouldIncludeSanitizedSourceLabelAndIndex()
	{
		BranchLinkNamingPolicy policy = new();

		string linkName = policy.BuildSourceLinkName("Asura-Scan!!", 2);

		Assert.Equal("10_source_Asura_Scan_002", linkName);
	}

	/// <summary>
	/// Verifies sanitization falls back to a placeholder when labels normalize to an empty value.
	/// </summary>
	[Fact]
	public void BuildAdditionalOverrideLinkName_Edge_ShouldUseFallbackLabel_WhenSanitizedLabelIsEmpty()
	{
		BranchLinkNamingPolicy policy = new();

		string linkName = policy.BuildAdditionalOverrideLinkName("/ssm/override/!!!", 0);

		Assert.Equal("01_override_x_000", linkName);
	}

	/// <summary>
	/// Verifies very large labels sanitize without stack overflow and produce deterministic output.
	/// </summary>
	[Fact]
	public void BuildSourceLinkName_Edge_ShouldHandleVeryLargeLabelsWithoutStackOverflow()
	{
		BranchLinkNamingPolicy policy = new();
		string largeLabel = new('A', 16_384);

		string linkName = policy.BuildSourceLinkName(largeLabel, 5);

		Assert.StartsWith("10_source_", linkName, StringComparison.Ordinal);
		Assert.EndsWith("_005", linkName, StringComparison.Ordinal);
		Assert.Contains(new string('A', 256), linkName, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies invalid label and index inputs throw argument-related exceptions.
	/// </summary>
	[Theory]
	[InlineData(null, 0)]
	[InlineData("", 0)]
	[InlineData(" ", 0)]
	[InlineData("/ssm/override/disk1", -1)]
	public void BuildAdditionalOverrideLinkName_Failure_ShouldThrow_WhenInputIsInvalid(
		string? overrideVolumeRootPath,
		int index)
	{
		BranchLinkNamingPolicy policy = new();

		Assert.ThrowsAny<ArgumentException>(() => policy.BuildAdditionalOverrideLinkName(overrideVolumeRootPath!, index));
	}
}
