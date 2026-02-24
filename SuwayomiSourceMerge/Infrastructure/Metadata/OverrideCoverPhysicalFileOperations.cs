namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Default filesystem adapter for override cover setup and write flows.
/// </summary>
internal sealed class OverrideCoverPhysicalFileOperations : IOverrideCoverFileOperations
{
	/// <inheritdoc />
	public void CreateDirectory(string path)
	{
		Directory.CreateDirectory(path);
	}

	/// <inheritdoc />
	public void WriteAllBytes(string path, byte[] bytes)
	{
		File.WriteAllBytes(path, bytes);
	}

	/// <inheritdoc />
	public void MoveFile(string sourcePath, string destinationPath, bool overwrite)
	{
		File.Move(sourcePath, destinationPath, overwrite);
	}

	/// <inheritdoc />
	public bool FileExists(string path)
	{
		return File.Exists(path);
	}

	/// <inheritdoc />
	public void DeleteFile(string path)
	{
		File.Delete(path);
	}
}
