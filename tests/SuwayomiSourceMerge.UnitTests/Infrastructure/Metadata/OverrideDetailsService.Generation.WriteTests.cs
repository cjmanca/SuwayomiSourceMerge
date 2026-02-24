namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Verifies temporary details write cleanup helper behavior.
/// </summary>
public sealed class OverrideDetailsServiceGenerationWriteTests
{
	/// <summary>
	/// Verifies cleanup exits without calling delete when the temporary file does not exist.
	/// </summary>
	[Fact]
	public void TryDeleteTemporaryFile_Expected_ShouldReturnWithoutDelete_WhenFileMissing()
	{
		bool deleteCalled = false;

		OverrideDetailsService.TryDeleteTemporaryFile(
			"C:\\temp\\missing.tmp",
			static _ => false,
			_ => deleteCalled = true);

		Assert.False(deleteCalled);
	}

	/// <summary>
	/// Verifies cleanup swallows known filesystem exceptions for deterministic best-effort behavior.
	/// </summary>
	/// <param name="exception">Known filesystem exception to throw from delete callback.</param>
	[Theory]
	[MemberData(nameof(KnownCleanupExceptions))]
	public void TryDeleteTemporaryFile_Edge_ShouldSwallowKnownFilesystemExceptions(Exception exception)
	{
		Exception? captured = Record.Exception(
			() =>
				OverrideDetailsService.TryDeleteTemporaryFile(
					"C:\\temp\\known-error.tmp",
					static _ => true,
					_ => throw exception));

		Assert.Null(captured);
	}

	/// <summary>
	/// Verifies cleanup rethrows unknown exceptions to avoid hiding unexpected failures.
	/// </summary>
	[Fact]
	public void TryDeleteTemporaryFile_Failure_ShouldRethrowUnknownExceptions()
	{
		Assert.Throws<InvalidOperationException>(
			() =>
				OverrideDetailsService.TryDeleteTemporaryFile(
					"C:\\temp\\unknown-error.tmp",
					static _ => true,
					static _ => throw new InvalidOperationException("Unexpected failure.")));
	}

	/// <summary>
	/// Gets known filesystem exceptions swallowed by best-effort cleanup.
	/// </summary>
	public static IEnumerable<object[]> KnownCleanupExceptions()
	{
		yield return [new IOException("io failure")];
		yield return [new UnauthorizedAccessException("access denied")];
		yield return [new NotSupportedException("unsupported path")];
		yield return [new PathTooLongException("path too long")];
	}
}
