namespace GtaIVModLoader.Core;

/// <summary>
/// Abstracts filesystem link operations (junctions, hardlinks, symlinks) and basic checks for testing.
/// </summary>
public interface IFileSystemLinker
{
    /// <summary>
    /// Creates a hard link from linkPath to targetPath.
    /// Same-volume only, does not require administrative elevation.
    /// </summary>
    void CreateHardLink(string linkPath, string targetPath);

    /// <summary>
    /// Creates a directory junction (reparse point) from junctionPath to targetPath.
    /// Supports cross-drive linking, does not require administrative elevation.
    /// </summary>
    void CreateJunction(string junctionPath, string targetPath);

    /// <summary>
    /// Creates a symbolic link from linkPath to targetPath.
    /// May require administrative privileges or Developer Mode on Windows.
    /// </summary>
    void CreateSymbolicLink(string linkPath, string targetPath, bool isDirectory);

    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Determines whether the specified directory exists.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Checks if the path points to a directory junction.
    /// </summary>
    bool IsJunction(string path);

    /// <summary>
    /// Checks if the path points to a symbolic link.
    /// </summary>
    bool IsSymbolicLink(string path);

    /// <summary>
    /// Deletes a file or link.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Deletes a directory junction or empty directory.
    /// </summary>
    void DeleteDirectory(string path);
}
