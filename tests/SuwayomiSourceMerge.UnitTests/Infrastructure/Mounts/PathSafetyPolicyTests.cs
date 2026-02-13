namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies path-safety escaping and strict-containment behavior for branch-planning paths.
/// </summary>
public sealed class PathSafetyPolicyTests
{
	/// <summary>
	/// Verifies non-reserved path segments are not modified.
	/// </summary>
	[Fact]
	public void EscapeReservedSegment_Expected_ShouldPreserveNonReservedSegment()
	{
		string escaped = PathSafetyPolicy.EscapeReservedSegment("Manga Title 1");

		Assert.Equal("Manga Title 1", escaped);
	}

	/// <summary>
	/// Verifies reserved dot-segment values are escaped deterministically.
	/// </summary>
	/// <param name="segment">Reserved segment value.</param>
	/// <param name="expectedEscapedSegment">Expected escaped replacement.</param>
	[Theory]
	[InlineData(".", "_ssm_dot_")]
	[InlineData("..", "_ssm_dotdot_")]
	public void EscapeReservedSegment_Edge_ShouldEscapeReservedDotSegments(
		string segment,
		string expectedEscapedSegment)
	{
		string escaped = PathSafetyPolicy.EscapeReservedSegment(segment);

		Assert.Equal(expectedEscapedSegment, escaped);
	}

	/// <summary>
	/// Verifies strict-child validation returns normalized child paths that stay under the root.
	/// </summary>
	[Fact]
	public void EnsureStrictChildPath_Expected_ShouldReturnNormalizedChildPath()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string rootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "root")).FullName;
		string candidatePath = Path.Combine(rootPath, "subdir", "..", "title");

		string resolved = PathSafetyPolicy.EnsureStrictChildPath(
			rootPath,
			candidatePath,
			nameof(candidatePath));

		Assert.Equal(Path.Combine(rootPath, "title"), resolved);
	}

	/// <summary>
	/// Verifies strict-child validation treats case-variant paths as contained on Windows.
	/// </summary>
	[Fact]
	public void EnsureStrictChildPath_Edge_ShouldTreatCaseVariantAsContained_OnWindows()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		using TemporaryDirectory temporaryDirectory = new();
		string rootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "root")).FullName;
		string candidatePath = Path.Combine(PathTestUtilities.InvertPathCase(rootPath), "Child");

		string resolved = PathSafetyPolicy.EnsureStrictChildPath(
			rootPath,
			candidatePath,
			nameof(candidatePath));

		Assert.EndsWith($"{Path.DirectorySeparatorChar}Child", resolved, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Verifies strict-child validation rejects candidates that resolve to the root path itself.
	/// </summary>
	[Fact]
	public void EnsureStrictChildPath_Failure_ShouldThrow_WhenCandidateEqualsRoot()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string rootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "root")).FullName;
		string candidatePath = Path.Combine(rootPath, ".");

		Assert.Throws<ArgumentException>(
			() => PathSafetyPolicy.EnsureStrictChildPath(
				rootPath,
				candidatePath,
				nameof(candidatePath)));
	}

	/// <summary>
	/// Verifies strict-child validation rejects candidates that resolve outside the root path.
	/// </summary>
	[Fact]
	public void EnsureStrictChildPath_Failure_ShouldThrow_WhenCandidateResolvesOutsideRoot()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string rootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "root")).FullName;
		string candidatePath = Path.Combine(rootPath, "..", "outside");

		Assert.Throws<ArgumentException>(
			() => PathSafetyPolicy.EnsureStrictChildPath(
				rootPath,
				candidatePath,
				nameof(candidatePath)));
	}

	/// <summary>
	/// Verifies strict-child validation rejects non-absolute root and candidate paths.
	/// </summary>
	[Fact]
	public void EnsureStrictChildPath_Failure_ShouldThrow_WhenPathsAreNotAbsolute()
	{
		Assert.Throws<ArgumentException>(
			() => PathSafetyPolicy.EnsureStrictChildPath(
				"relative-root",
				"/absolute/candidate",
				"candidatePath"));

		string fullyQualifiedRootPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ssm-root"));
		Assert.Throws<ArgumentException>(
			() => PathSafetyPolicy.EnsureStrictChildPath(
				fullyQualifiedRootPath,
				"relative-candidate",
				"candidatePath"));
	}

	/// <summary>
	/// Verifies candidate-path root validation failures use the caller-provided parameter name.
	/// </summary>
	[Fact]
	public void EnsureStrictChildPath_Failure_ShouldUseCallerProvidedParamName_ForCandidatePathValidation()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string fullyQualifiedRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "root")).FullName;

		ArgumentException exception = Assert.Throws<ArgumentException>(
			() => PathSafetyPolicy.EnsureStrictChildPath(
				fullyQualifiedRootPath,
				"relative-candidate",
				"linkName"));

		Assert.Equal("linkName", exception.ParamName);
	}

	/// <summary>
	/// Verifies strict-child validation rejects Windows root-relative candidate paths.
	/// </summary>
	[Fact]
	public void EnsureStrictChildPath_Failure_ShouldThrow_WhenCandidateIsWindowsRootRelative()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		using TemporaryDirectory temporaryDirectory = new();
		string fullyQualifiedRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "root")).FullName;

		ArgumentException exception = Assert.Throws<ArgumentException>(
			() => PathSafetyPolicy.EnsureStrictChildPath(
				fullyQualifiedRootPath,
				@"\root-relative",
				"candidatePath"));

		Assert.Equal("candidatePath", exception.ParamName);
	}

	/// <summary>
	/// Verifies strict-child validation rejects Windows drive-relative candidate paths.
	/// </summary>
	[Fact]
	public void EnsureStrictChildPath_Failure_ShouldThrow_WhenCandidateIsWindowsDriveRelative()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		using TemporaryDirectory temporaryDirectory = new();
		string fullyQualifiedRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "root")).FullName;
		string driveRelativePath = $"{fullyQualifiedRootPath[0]}:drive-relative";

		ArgumentException exception = Assert.Throws<ArgumentException>(
			() => PathSafetyPolicy.EnsureStrictChildPath(
				fullyQualifiedRootPath,
				driveRelativePath,
				"candidatePath"));

		Assert.Equal("candidatePath", exception.ParamName);
	}

	/// <summary>
	/// Verifies path-equality checks use OS-aware semantics after normalization.
	/// </summary>
	[Fact]
	public void ArePathsEqual_Expected_ShouldCompareUsingOsAwareSemantics()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string rootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "root")).FullName;
		string canonicalPath = Path.Combine(rootPath, "title");
		string variantPath = OperatingSystem.IsWindows()
			? PathTestUtilities.InvertPathCase(canonicalPath)
			: canonicalPath;

		bool areEqual = PathSafetyPolicy.ArePathsEqual(
			canonicalPath,
			Path.Combine(variantPath, "."));

		Assert.True(areEqual);
	}

	/// <summary>
	/// Verifies separator detection returns expected results for slash and backslash inputs.
	/// </summary>
	[Theory]
	[InlineData("MangaTitle", false)]
	[InlineData("Manga/Title", true)]
	[InlineData(@"Manga\Title", true)]
	public void ContainsDirectorySeparator_Expected_ShouldDetectSeparators(string value, bool expectedContainsSeparator)
	{
		bool containsSeparator = PathSafetyPolicy.ContainsDirectorySeparator(value);

		Assert.Equal(expectedContainsSeparator, containsSeparator);
	}

	/// <summary>
	/// Verifies path comparer aligns with operating-system path-equality semantics.
	/// </summary>
	[Fact]
	public void GetPathComparer_Expected_ShouldAlignWithOperatingSystemSemantics()
	{
		StringComparer comparer = PathSafetyPolicy.GetPathComparer();
		string first = "abc";
		string second = "ABC";

		if (OperatingSystem.IsWindows())
		{
			Assert.True(comparer.Equals(first, second));
			return;
		}

		Assert.False(comparer.Equals(first, second));
	}

	/// <summary>
	/// Verifies link-name segment validation accepts safe values.
	/// </summary>
	[Fact]
	public void ValidateLinkNameSegment_Expected_ShouldAcceptSafeValue()
	{
		PathSafetyPolicy.ValidateLinkNameSegment("safe_link-name_01", "linkName");
	}

	/// <summary>
	/// Verifies link-name segment validation rejects deterministic invalid values and reports the caller parameter name.
	/// </summary>
	/// <param name="value">Link-name value under test.</param>
	[Theory]
	[InlineData("bad/name")]
	[InlineData("bad\\name")]
	[InlineData(".")]
	[InlineData("..")]
	[InlineData("name:bad")]
	[InlineData("name*bad")]
	[InlineData("name?bad")]
	[InlineData("name\"bad")]
	[InlineData("name<bad")]
	[InlineData("name>bad")]
	[InlineData("name|bad")]
	[InlineData("bad\tname")]
	[InlineData("bad.")]
	[InlineData("bad ")]
	public void ValidateLinkNameSegment_Failure_ShouldThrowForInvalidValue(string value)
	{
		ArgumentException exception = Assert.Throws<ArgumentException>(
			() => PathSafetyPolicy.ValidateLinkNameSegment(value, "linkName"));

		Assert.Equal("linkName", exception.ParamName);
	}
}
