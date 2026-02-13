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
	/// Verifies very large source labels sanitize without stack overflow and fit filesystem component limits.
	/// </summary>
	[Fact]
	public void BuildSourceLinkName_Edge_ShouldHandleVeryLargeLabelsWithoutStackOverflow()
	{
		BranchLinkNamingPolicy policy = new();
		string largeLabel = new('A', 16_384);

		string linkName = policy.BuildSourceLinkName(largeLabel, 5);
		string repeatedLinkName = policy.BuildSourceLinkName(largeLabel, 5);
		string sanitizedLabel = linkName["10_source_".Length..^4];

		Assert.StartsWith("10_source_", linkName, StringComparison.Ordinal);
		Assert.EndsWith("_005", linkName, StringComparison.Ordinal);
		Assert.Equal(repeatedLinkName, linkName);
		Assert.True(linkName.Length <= 255);
		Assert.Matches("^[A]+_[0-9a-f]{12}$", sanitizedLabel);
	}

	/// <summary>
	/// Verifies very large override volume labels are hash-truncated deterministically to fit component limits.
	/// </summary>
	[Fact]
	public void BuildAdditionalOverrideLinkName_Edge_ShouldHashTruncateVeryLargeVolumeLabels()
	{
		BranchLinkNamingPolicy policy = new();
		string largeVolumePath = $"/ssm/override/{new string('b', 16_384)}";

		string linkName = policy.BuildAdditionalOverrideLinkName(largeVolumePath, 9);
		string repeatedLinkName = policy.BuildAdditionalOverrideLinkName(largeVolumePath, 9);
		string sanitizedLabel = linkName["01_override_".Length..^4];

		Assert.StartsWith("01_override_", linkName, StringComparison.Ordinal);
		Assert.EndsWith("_009", linkName, StringComparison.Ordinal);
		Assert.Equal(repeatedLinkName, linkName);
		Assert.True(linkName.Length <= 255);
		Assert.Matches("^[b]+_[0-9a-f]{12}$", sanitizedLabel);
	}

	/// <summary>
	/// Verifies long labels sharing the same prefix but different tails produce different hash-truncated link names.
	/// </summary>
	[Fact]
	public void BuildSourceLinkName_Edge_ShouldProduceDistinctHashTruncation_ForDifferentLongLabels()
	{
		BranchLinkNamingPolicy policy = new();
		string sharedPrefix = new('X', 4_096);
		string firstLabel = $"{sharedPrefix}A-tail";
		string secondLabel = $"{sharedPrefix}B-tail";

		string firstLinkName = policy.BuildSourceLinkName(firstLabel, 1);
		string secondLinkName = policy.BuildSourceLinkName(secondLabel, 1);

		Assert.NotEqual(firstLinkName, secondLinkName);
		Assert.True(firstLinkName.Length <= 255);
		Assert.True(secondLinkName.Length <= 255);
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
