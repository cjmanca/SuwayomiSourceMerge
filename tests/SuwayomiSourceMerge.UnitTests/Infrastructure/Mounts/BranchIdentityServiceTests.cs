namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Verifies deterministic branch identity hashing behavior.
/// </summary>
public sealed class BranchIdentityServiceTests
{
	/// <summary>
	/// Verifies group ids are generated from SHA-256 prefixes using lowercase hexadecimal output.
	/// </summary>
	[Fact]
	public void BuildGroupId_Expected_ShouldReturnDeterministicLowercaseShaPrefix()
	{
		BranchIdentityService service = new();

		string groupId = service.BuildGroupId("group-key-1");

		Assert.Equal("7aa98438a05a7ea3", groupId);
	}

	/// <summary>
	/// Verifies branch hashes are generated from SHA-256 prefixes using lowercase hexadecimal output.
	/// </summary>
	[Fact]
	public void BuildBranchHash_Expected_ShouldReturnDeterministicLowercaseShaPrefix()
	{
		BranchIdentityService service = new();

		string branchHash = service.BuildBranchHash("branch-spec-1");

		Assert.Equal("b9580151eb9c", branchHash);
	}

	/// <summary>
	/// Verifies desired identities combine the expected prefix, group id, and branch hash components.
	/// </summary>
	[Fact]
	public void BuildDesiredIdentity_Expected_ShouldComposeIdentityFromGroupAndBranchHash()
	{
		BranchIdentityService service = new();

		string identity = service.BuildDesiredIdentity("group-key-1", "/ssm/branches/a=RW:/ssm/source=RO");

		Assert.Equal("suwayomi_7aa98438a05a7ea3_bbdcb7cf1b8e", identity);
	}

	/// <summary>
	/// Verifies repeated invocations for identical input produce identical outputs.
	/// </summary>
	[Fact]
	public void BuildDesiredIdentity_Edge_ShouldRemainDeterministicAcrossRepeatedCalls()
	{
		BranchIdentityService service = new();

		string first = service.BuildDesiredIdentity("demo-group", "branch-spec");
		string second = service.BuildDesiredIdentity("demo-group", "branch-spec");

		Assert.Equal(first, second);
	}

	/// <summary>
	/// Verifies identity generation rejects invalid group-key and branch-specification values.
	/// </summary>
	[Theory]
	[InlineData(null, "branch-spec")]
	[InlineData("", "branch-spec")]
	[InlineData(" ", "branch-spec")]
	[InlineData("group", null)]
	[InlineData("group", "")]
	[InlineData("group", " ")]
	public void BuildDesiredIdentity_Failure_ShouldThrow_WhenInputIsInvalid(
		string? groupKey,
		string? branchSpecification)
	{
		BranchIdentityService service = new();

		Assert.ThrowsAny<ArgumentException>(() => service.BuildDesiredIdentity(groupKey!, branchSpecification!));
	}
}
