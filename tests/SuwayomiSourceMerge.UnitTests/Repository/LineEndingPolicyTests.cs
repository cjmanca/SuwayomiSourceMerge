namespace SuwayomiSourceMerge.UnitTests.Repository;

using System.Diagnostics;
using System.Text;

/// <summary>
/// Verifies tracked repository text content remains LF-only.
/// </summary>
public sealed class LineEndingPolicyTests
{
	/// <summary>
	/// Binary extensions excluded from line-ending checks.
	/// </summary>
	private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".7z",
		".bmp",
		".dll",
		".exe",
		".gif",
		".gz",
		".ico",
		".jpeg",
		".jpg",
		".mp3",
		".mp4",
		".pdf",
		".png",
		".rar",
		".snk",
		".so",
		".tar",
		".tgz",
		".webp",
		".zip"
	};

	/// <summary>
	/// Ensures tracked text blobs do not contain CRLF or lone CR line endings.
	/// </summary>
	[Fact]
	public void TrackedTextFiles_ShouldUseLfLineEndingsOnly()
	{
		string repositoryRoot = FindRepositoryRoot();
		IReadOnlyList<TrackedFileBlob> trackedFiles = GetTrackedFiles(repositoryRoot);

		Assert.NotEmpty(trackedFiles);

		Dictionary<string, byte[]> blobCache = new(StringComparer.Ordinal);
		List<string> filesContainingCrLf = [];
		List<string> filesContainingLoneCr = [];

		foreach (TrackedFileBlob trackedFile in trackedFiles)
		{
			string extension = Path.GetExtension(trackedFile.Path);
			if (BinaryExtensions.Contains(extension))
			{
				continue;
			}

			if (!blobCache.TryGetValue(trackedFile.ObjectId, out byte[]? blobBytes))
			{
				blobBytes = ExecuteGitAndCaptureBytes(repositoryRoot, $"cat-file -p {trackedFile.ObjectId}");
				blobCache[trackedFile.ObjectId] = blobBytes;
			}

			if (blobBytes.Length == 0 || IsBinaryContent(blobBytes))
			{
				continue;
			}

			if (ContainsCrLf(blobBytes))
			{
				filesContainingCrLf.Add(trackedFile.Path);
				continue;
			}

			if (ContainsLoneCr(blobBytes))
			{
				filesContainingLoneCr.Add(trackedFile.Path);
			}
		}

		bool hasViolations = filesContainingCrLf.Count > 0 || filesContainingLoneCr.Count > 0;
		Assert.True(!hasViolations, BuildFailureMessage(filesContainingCrLf, filesContainingLoneCr));
	}

	/// <summary>
	/// Locates the repository root by walking upward from the test output directory.
	/// </summary>
	/// <returns>Absolute repository root path.</returns>
	/// <exception cref="InvalidOperationException">Thrown when repository root markers are not found.</exception>
	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);

		while (directory is not null)
		{
			string candidateRoot = directory.FullName;
			bool hasSolution = File.Exists(Path.Combine(candidateRoot, "SuwayomiSourceMerge.slnx"));
			bool hasGitDirectory = Directory.Exists(Path.Combine(candidateRoot, ".git"));
			bool hasGitFile = File.Exists(Path.Combine(candidateRoot, ".git"));

			if (hasSolution && (hasGitDirectory || hasGitFile))
			{
				return candidateRoot;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException("Could not locate repository root from test output directory.");
	}

	/// <summary>
	/// Enumerates tracked files and their index object ids.
	/// </summary>
	/// <param name="repositoryRoot">Absolute repository root path.</param>
	/// <returns>Tracked file metadata from the Git index.</returns>
	/// <exception cref="FormatException">Thrown when Git stage output cannot be parsed.</exception>
	private static IReadOnlyList<TrackedFileBlob> GetTrackedFiles(string repositoryRoot)
	{
		string output = ExecuteGitAndCaptureText(repositoryRoot, "ls-files --stage -z");
		string[] records = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
		List<TrackedFileBlob> trackedFiles = new(records.Length);

		foreach (string record in records)
		{
			int tabIndex = record.IndexOf('\t', StringComparison.Ordinal);
			if (tabIndex <= 0 || tabIndex == record.Length - 1)
			{
				throw new FormatException($"Unexpected git ls-files record format: \"{record}\".");
			}

			string metadata = record[..tabIndex];
			string path = record[(tabIndex + 1)..];
			string[] metadataParts = metadata.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (metadataParts.Length < 3)
			{
				throw new FormatException($"Unexpected git ls-files metadata format: \"{metadata}\".");
			}

			string objectId = metadataParts[1];
			trackedFiles.Add(new TrackedFileBlob(path, objectId));
		}

		return trackedFiles;
	}

	/// <summary>
	/// Runs a git command and captures UTF-8 output.
	/// </summary>
	/// <param name="repositoryRoot">Absolute repository root path.</param>
	/// <param name="arguments">Git command arguments.</param>
	/// <returns>Standard output text.</returns>
	/// <exception cref="InvalidOperationException">Thrown when git command start or execution fails.</exception>
	private static string ExecuteGitAndCaptureText(string repositoryRoot, string arguments)
	{
		byte[] bytes = ExecuteGitAndCaptureBytes(repositoryRoot, arguments);
		return Encoding.UTF8.GetString(bytes);
	}

	/// <summary>
	/// Runs a git command and captures raw standard output bytes.
	/// </summary>
	/// <param name="repositoryRoot">Absolute repository root path.</param>
	/// <param name="arguments">Git command arguments.</param>
	/// <returns>Raw standard output bytes.</returns>
	/// <exception cref="InvalidOperationException">Thrown when git command start or execution fails.</exception>
	private static byte[] ExecuteGitAndCaptureBytes(string repositoryRoot, string arguments)
	{
		ProcessStartInfo startInfo = new()
		{
			FileName = "git",
			Arguments = arguments,
			WorkingDirectory = repositoryRoot,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		using Process? process = Process.Start(startInfo);
		if (process is null)
		{
			throw new InvalidOperationException($"Failed to start git process for command: git {arguments}");
		}

		using MemoryStream outputStream = new();
		process.StandardOutput.BaseStream.CopyTo(outputStream);
		string error = process.StandardError.ReadToEnd();
		process.WaitForExit();

		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException(
				$"git {arguments} failed with exit code {process.ExitCode}: {error}");
		}

		return outputStream.ToArray();
	}

	/// <summary>
	/// Determines whether the blob content should be treated as binary.
	/// </summary>
	/// <param name="blobBytes">Blob bytes to inspect.</param>
	/// <returns><see langword="true"/> when binary-like content is detected.</returns>
	private static bool IsBinaryContent(ReadOnlySpan<byte> blobBytes)
	{
		for (int index = 0; index < blobBytes.Length; index++)
		{
			if (blobBytes[index] == 0)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether content contains CRLF sequences.
	/// </summary>
	/// <param name="blobBytes">Blob bytes to inspect.</param>
	/// <returns><see langword="true"/> when CRLF is present.</returns>
	private static bool ContainsCrLf(ReadOnlySpan<byte> blobBytes)
	{
		for (int index = 0; index < blobBytes.Length - 1; index++)
		{
			if (blobBytes[index] == '\r' && blobBytes[index + 1] == '\n')
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether content contains carriage returns not followed by line feeds.
	/// </summary>
	/// <param name="blobBytes">Blob bytes to inspect.</param>
	/// <returns><see langword="true"/> when lone carriage returns are present.</returns>
	private static bool ContainsLoneCr(ReadOnlySpan<byte> blobBytes)
	{
		for (int index = 0; index < blobBytes.Length; index++)
		{
			if (blobBytes[index] != '\r')
			{
				continue;
			}

			bool hasFollowingLf = index < blobBytes.Length - 1 && blobBytes[index + 1] == '\n';
			if (!hasFollowingLf)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Builds a deterministic assertion message for line-ending violations.
	/// </summary>
	/// <param name="filesContainingCrLf">Files with CRLF sequences.</param>
	/// <param name="filesContainingLoneCr">Files with lone carriage returns.</param>
	/// <returns>Assertion failure message.</returns>
	private static string BuildFailureMessage(
		IReadOnlyCollection<string> filesContainingCrLf,
		IReadOnlyCollection<string> filesContainingLoneCr)
	{
		List<string> lines =
		[
			"Repository line-ending policy violation detected in tracked text blobs."
		];

		if (filesContainingCrLf.Count > 0)
		{
			lines.Add("Files containing CRLF:");
			foreach (string path in filesContainingCrLf.OrderBy(path => path, StringComparer.Ordinal))
			{
				lines.Add($" - {path}");
			}
		}

		if (filesContainingLoneCr.Count > 0)
		{
			lines.Add("Files containing lone CR:");
			foreach (string path in filesContainingLoneCr.OrderBy(path => path, StringComparer.Ordinal))
			{
				lines.Add($" - {path}");
			}
		}

		return string.Join(Environment.NewLine, lines);
	}

	/// <summary>
	/// Tracked file metadata containing the path and blob object id.
	/// </summary>
	/// <param name="Path">Repository-relative path from git ls-files.</param>
	/// <param name="ObjectId">Git blob object id from git ls-files stage output.</param>
	private sealed record TrackedFileBlob(string Path, string ObjectId);
}
