using System.Security.Cryptography;
using System.Text;

namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Produces deterministic mergerfs branch identity values from group keys and branch specifications.
/// </summary>
internal sealed class BranchIdentityService
{
	/// <summary>
	/// Prefix used for desired identity tokens.
	/// </summary>
	private const string IdentityPrefix = "suwayomi";

	/// <summary>
	/// Number of hexadecimal characters in the generated group id.
	/// </summary>
	private const int GroupIdLength = 16;

	/// <summary>
	/// Number of hexadecimal characters in the generated branch hash.
	/// </summary>
	private const int BranchHashLength = 12;

	/// <summary>
	/// Builds the deterministic group id from the provided group key.
	/// </summary>
	/// <param name="groupKey">Group key text to hash.</param>
	/// <returns>Deterministic hexadecimal group id.</returns>
	public string BuildGroupId(string groupKey)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);
		return BuildShortHash(groupKey, GroupIdLength);
	}

	/// <summary>
	/// Builds the deterministic branch hash from the provided branch specification.
	/// </summary>
	/// <param name="branchSpecification">Branch specification text to hash.</param>
	/// <returns>Deterministic hexadecimal branch hash.</returns>
	public string BuildBranchHash(string branchSpecification)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(branchSpecification);
		return BuildShortHash(branchSpecification, BranchHashLength);
	}

	/// <summary>
	/// Builds the deterministic desired identity token used for mount identity comparison.
	/// </summary>
	/// <param name="groupKey">Group key text.</param>
	/// <param name="branchSpecification">Deterministic branch specification string.</param>
	/// <returns>Desired identity token in <c>suwayomi_&lt;group&gt;_&lt;hash&gt;</c> format.</returns>
	public string BuildDesiredIdentity(string groupKey, string branchSpecification)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(branchSpecification);

		string groupId = BuildGroupId(groupKey);
		string branchHash = BuildBranchHash(branchSpecification);
		return $"{IdentityPrefix}_{groupId}_{branchHash}";
	}

	/// <summary>
	/// Builds a lowercase hexadecimal SHA-256 hash prefix of the requested length.
	/// </summary>
	/// <param name="input">Input text to hash.</param>
	/// <param name="length">Length of the hexadecimal prefix to return.</param>
	/// <returns>Lowercase hexadecimal hash prefix.</returns>
	private static string BuildShortHash(string input, int length)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(input);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

		byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		string hashText = Convert.ToHexString(hashBytes).ToLowerInvariant();
		return hashText[..length];
	}
}
