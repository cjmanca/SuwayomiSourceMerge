using SuwayomiSourceMerge.Infrastructure.Mounts;

namespace SuwayomiSourceMerge.Application.Mounting;

/// <summary>
/// Merged-root residual cleanup behavior for <see cref="MergeMountWorkflow"/>.
/// </summary>
internal sealed partial class MergeMountWorkflow
{
	/// <summary>
	/// Config subdirectory used to store cleanup artifacts.
	/// </summary>
	private const string CleanupArtifactsDirectoryName = "cleanup";

	/// <summary>
	/// Cleanup artifact subdirectory used to quarantine residual merged directories.
	/// </summary>
	private const string MergedResidualDirectoryName = "merged-residual";

	/// <summary>
	/// Cleans residual directories under the merged root after unmount attempts.
	/// </summary>
	/// <param name="phase">Cleanup phase name for diagnostics.</param>
	/// <param name="stillMountedManagedMountPoints">Managed mountpoints still active after unmount attempts.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Counts and skip state for merged-root cleanup behavior.</returns>
	private (int RemovedEmptyDirectories, int MovedNonEmptyDirectories, bool SkippedDueToActiveMounts) CleanupMergedRootDirectories(
		string phase,
		IReadOnlySet<string> stillMountedManagedMountPoints,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(phase);
		ArgumentNullException.ThrowIfNull(stillMountedManagedMountPoints);

		if (stillMountedManagedMountPoints.Count > 0)
		{
			_logger.Warning(
				MergeCleanupEvent,
				"Skipped merged-root directory cleanup because managed mountpoints remain active after unmount attempts.",
				BuildContext(
					("phase", phase),
					("still_mounted_managed_mounts", stillMountedManagedMountPoints.Count.ToString())));
			return (0, 0, true);
		}

		string mergedRootPath = Path.GetFullPath(_options.MergedRootPath);
		if (!Directory.Exists(mergedRootPath))
		{
			return (0, 0, false);
		}

		int removedEmptyDirectoryCount = 0;
		int movedNonEmptyDirectoryCount = 0;

		string[] mergedDirectories = EnumerateMergedRootDirectoriesDeepestFirst(mergedRootPath, phase, cancellationToken);
		for (int index = 0; index < mergedDirectories.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string directoryPath = mergedDirectories[index];
			try
			{
				Directory.Delete(directoryPath, recursive: false);
				removedEmptyDirectoryCount++;
			}
			catch (DirectoryNotFoundException)
			{
			}
			catch (IOException)
			{
				// Expected for non-empty directories or directories still in use.
			}
			catch (UnauthorizedAccessException)
			{
				// Expected for inaccessible directories; leave in place for later quarantine handling.
			}
		}

		string[] topLevelMergedDirectories;
		try
		{
			// Directory-only scope by policy: root-level files under merged are left untouched.
			topLevelMergedDirectories = Directory.GetDirectories(mergedRootPath)
				.OrderBy(static path => path, PathSafetyPolicy.GetPathComparer())
				.ToArray();
		}
		catch (Exception exception)
		{
			_logger.Warning(
				MergeCleanupEvent,
				"Failed to enumerate merged-root directories for residual cleanup.",
				BuildContext(
					("phase", phase),
					("merged_root_path", mergedRootPath),
					("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
					("message", exception.Message)));
			return (removedEmptyDirectoryCount, movedNonEmptyDirectoryCount, false);
		}

		string? quarantineBatchDirectoryPath = null;
		for (int index = 0; index < topLevelMergedDirectories.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string mergedDirectoryPath = topLevelMergedDirectories[index];

			if (!TryHasFileSystemEntries(mergedDirectoryPath, phase, out bool hasEntries))
			{
				continue;
			}

			if (!hasEntries)
			{
				try
				{
					Directory.Delete(mergedDirectoryPath, recursive: false);
					removedEmptyDirectoryCount++;
				}
				catch (Exception)
				{
					// Best-effort final empty delete; leave in place if it cannot be removed.
				}

				continue;
			}

			quarantineBatchDirectoryPath ??= EnsureMergedResidualQuarantineBatchDirectory(phase);
			string destinationPath = BuildUniqueDirectoryDestinationPath(quarantineBatchDirectoryPath, Path.GetFileName(mergedDirectoryPath));
			if (!TryMoveDirectory(mergedDirectoryPath, destinationPath, cancellationToken, out string relocationMode))
			{
				_logger.Warning(
					MergeCleanupEvent,
					"Failed to quarantine non-empty merged directory.",
					BuildContext(
						("phase", phase),
						("source_path", mergedDirectoryPath),
						("destination_path", destinationPath)));
				continue;
			}

			movedNonEmptyDirectoryCount++;
			_logger.Warning(
				MergeCleanupEvent,
				"Moved non-empty merged directory into config cleanup quarantine.",
				BuildContext(
					("phase", phase),
					("source_path", mergedDirectoryPath),
					("destination_path", destinationPath),
					("relocation_mode", relocationMode)));
		}

		return (removedEmptyDirectoryCount, movedNonEmptyDirectoryCount, false);
	}

	/// <summary>
	/// Enumerates merged-root descendant directories in deepest-first order.
	/// </summary>
	/// <param name="mergedRootPath">Merged root path.</param>
	/// <param name="phase">Cleanup phase for diagnostics.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Discovered directories sorted deepest-first.</returns>
	private string[] EnumerateMergedRootDirectoriesDeepestFirst(
		string mergedRootPath,
		string phase,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mergedRootPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(phase);

		List<string> discoveredDirectories = [];
		Stack<string> pending = new();
		pending.Push(mergedRootPath);

		while (pending.Count > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string currentDirectory = pending.Pop();
			string[] childDirectories;
			try
			{
				childDirectories = Directory.GetDirectories(currentDirectory);
			}
			catch (Exception exception)
			{
				_logger.Warning(
					MergeCleanupEvent,
					"Failed to enumerate one merged-root cleanup directory.",
					BuildContext(
						("phase", phase),
						("directory_path", currentDirectory),
						("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
						("message", exception.Message)));
				continue;
			}

			for (int index = 0; index < childDirectories.Length; index++)
			{
				string childDirectoryPath = childDirectories[index];
				discoveredDirectories.Add(childDirectoryPath);
				pending.Push(childDirectoryPath);
			}
		}

		return discoveredDirectories
			.OrderByDescending(GetPathDepth)
			.ThenBy(static path => path, PathSafetyPolicy.GetPathComparer())
			.ToArray();
	}

	/// <summary>
	/// Returns whether one directory currently contains entries.
	/// </summary>
	/// <param name="directoryPath">Directory path.</param>
	/// <param name="phase">Cleanup phase name for diagnostics.</param>
	/// <param name="hasEntries">Populated with entry state when successful.</param>
	/// <returns><see langword="true"/> when enumeration succeeds.</returns>
	private bool TryHasFileSystemEntries(string directoryPath, string phase, out bool hasEntries)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(phase);

		hasEntries = false;
		try
		{
			hasEntries = Directory.EnumerateFileSystemEntries(directoryPath).Any();
			return true;
		}
		catch (Exception exception)
		{
			_logger.Warning(
				MergeCleanupEvent,
				"Failed to inspect merged directory entry state during cleanup.",
				BuildContext(
					("phase", phase),
					("directory_path", directoryPath),
					("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
					("message", exception.Message)));
			return false;
		}
	}

	/// <summary>
	/// Ensures a quarantine batch directory exists for merged residual cleanup output.
	/// </summary>
	/// <param name="phase">Cleanup phase name.</param>
	/// <returns>Absolute quarantine batch directory path.</returns>
	private string EnsureMergedResidualQuarantineBatchDirectory(string phase)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(phase);

		string quarantineRootPath = Path.Combine(
			_options.ConfigRootPath,
			CleanupArtifactsDirectoryName,
			MergedResidualDirectoryName);
		Directory.CreateDirectory(quarantineRootPath);

		string safePhase = PathSafetyPolicy.EscapeReservedSegment(phase);
		string batchDirectoryName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{safePhase}_{Guid.NewGuid():N}".ToLowerInvariant();
		string batchDirectoryPath = Path.Combine(quarantineRootPath, batchDirectoryName);
		Directory.CreateDirectory(batchDirectoryPath);
		return batchDirectoryPath;
	}

	/// <summary>
	/// Builds a unique destination directory path under one parent.
	/// </summary>
	/// <param name="destinationParentPath">Destination parent directory.</param>
	/// <param name="sourceDirectoryName">Source directory name.</param>
	/// <returns>Unique destination directory path.</returns>
	private static string BuildUniqueDirectoryDestinationPath(string destinationParentPath, string sourceDirectoryName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationParentPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectoryName);

		string safeDirectoryName = PathSafetyPolicy.EscapeReservedSegment(sourceDirectoryName);
		string candidatePath = Path.Combine(destinationParentPath, safeDirectoryName);
		int suffix = 1;
		while (Directory.Exists(candidatePath) || File.Exists(candidatePath))
		{
			candidatePath = Path.Combine(destinationParentPath, $"{safeDirectoryName}_{suffix}");
			suffix++;
		}

		return candidatePath;
	}

	/// <summary>
	/// Moves one directory and falls back to copy/delete when direct move crosses filesystem boundaries.
	/// </summary>
	/// <param name="sourceDirectoryPath">Source directory path.</param>
	/// <param name="destinationDirectoryPath">Destination directory path.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <param name="relocationMode">Relocation mode description.</param>
	/// <returns><see langword="true"/> when relocation succeeds.</returns>
	private bool TryMoveDirectory(
		string sourceDirectoryPath,
		string destinationDirectoryPath,
		CancellationToken cancellationToken,
		out string relocationMode)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectoryPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectoryPath);

		relocationMode = string.Empty;
		try
		{
			Directory.Move(sourceDirectoryPath, destinationDirectoryPath);
			relocationMode = "move";
			return true;
		}
		catch (IOException)
		{
			try
			{
				CopyDirectoryRecursive(sourceDirectoryPath, destinationDirectoryPath, cancellationToken);
				Directory.Delete(sourceDirectoryPath, recursive: true);
				relocationMode = "copy_delete";
				return true;
			}
			catch (Exception exception)
			{
				_logger.Warning(
					MergeCleanupEvent,
					"Failed to relocate merged directory using copy/delete fallback.",
					BuildContext(
						("source_path", sourceDirectoryPath),
						("destination_path", destinationDirectoryPath),
						("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
						("message", exception.Message)));
				return false;
			}
		}
		catch (Exception exception)
		{
			_logger.Warning(
				MergeCleanupEvent,
				"Failed to relocate merged directory.",
				BuildContext(
					("source_path", sourceDirectoryPath),
					("destination_path", destinationDirectoryPath),
					("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
					("message", exception.Message)));
			return false;
		}
	}

	/// <summary>
	/// Copies one directory tree to a destination directory.
	/// </summary>
	/// <param name="sourceDirectoryPath">Source directory path.</param>
	/// <param name="destinationDirectoryPath">Destination directory path.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	private static void CopyDirectoryRecursive(
		string sourceDirectoryPath,
		string destinationDirectoryPath,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectoryPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectoryPath);

		Directory.CreateDirectory(destinationDirectoryPath);
		string[] files = Directory.GetFiles(sourceDirectoryPath);
		for (int index = 0; index < files.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string sourceFilePath = files[index];
			string destinationFilePath = Path.Combine(destinationDirectoryPath, Path.GetFileName(sourceFilePath));
			File.Copy(sourceFilePath, destinationFilePath, overwrite: false);
		}

		string[] childDirectories = Directory.GetDirectories(sourceDirectoryPath);
		for (int index = 0; index < childDirectories.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string childSourceDirectoryPath = childDirectories[index];
			string childDestinationDirectoryPath = Path.Combine(destinationDirectoryPath, Path.GetFileName(childSourceDirectoryPath));
			CopyDirectoryRecursive(childSourceDirectoryPath, childDestinationDirectoryPath, cancellationToken);
		}
	}
}
