using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace ManagerIV.Core;

/// <summary>
/// Handles safe archive extraction for ZIP, RAR, and 7Z formats, implementing zip-slip protection.
/// </summary>
public class ArchiveHandler
{
    /// <summary>
    /// Extracts the specified archive to the destination directory asynchronously.
    /// </summary>
    public async Task ExtractAsync(string archivePath, string destinationDir)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Archive file not found.", archivePath);
        }

        string fullDestinationPath = Path.GetFullPath(destinationDir);
        
        // Ensure destination exists
        if (!Directory.Exists(fullDestinationPath))
        {
            Directory.CreateDirectory(fullDestinationPath);
        }

        // Run extraction on background thread to keep UI responsive
        await Task.Run(() => Extract(archivePath, fullDestinationPath));
    }

    /// <summary>
    /// Detects if the extracted mod contents are wrapped inside a single subdirectory (or an update/ prefix),
    /// and promotes those files to the destination root, cleaning up any nested folders.
    /// </summary>
    public void PromoteModRoot(string destinationDir)
    {
        string rootPath = DetermineModRoot(destinationDir);
        if (rootPath == destinationDir)
        {
            return;
        }

        string tempPath = Path.Combine(Path.GetDirectoryName(destinationDir)!, "temp_promote_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.Move(rootPath, tempPath);
            if (Directory.Exists(destinationDir))
            {
                Directory.Delete(destinationDir, true);
            }
            Directory.Move(tempPath, destinationDir);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to promote mod root: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(tempPath))
            {
                try { Directory.Delete(tempPath, true); } catch { }
            }
        }
    }

    private string DetermineModRoot(string dir)
    {
        string[] files = Directory.GetFiles(dir);
        string[] dirs = Directory.GetDirectories(dir);
        
        var significantFiles = files.Where(f => {
            string name = Path.GetFileName(f).ToLowerInvariant();
            return name != ".ds_store" && name != "desktop.ini";
        }).ToList();

        if (dirs.Length == 1 && significantFiles.Count == 0)
        {
            return DetermineModRoot(dirs[0]);
        }
        
        foreach (var subDir in dirs)
        {
            string name = Path.GetFileName(subDir).ToLowerInvariant();
            if (name == "pc" || name == "common" || name == "tbogt" || name == "tlad" || name == "plugins" || name == "scripts")
            {
                return dir;
            }
        }
        
        return dir;
    }

    private void Extract(string archivePath, string destinationDir)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        
        foreach (var entry in archive.Entries)
        {
            if (entry.IsDirectory)
            {
                continue;
            }

            string? entryKey = entry.Key;
            if (string.IsNullOrEmpty(entryKey))
            {
                continue;
            }

            // Zip-slip/path-traversal protection:
            // 1. Reject paths containing directory traversal ("..")
            // 2. Reject absolute paths inside the archive key
            if (Path.IsPathRooted(entryKey) || entryKey.Contains(".."))
            {
                throw new InvalidOperationException(
                    $"Potential Zip-Slip attack detected: Archive entry '{entryKey}' contains path traversal sequences."
                );
            }

            // Combine destination with entry key and resolve absolute path
            string targetFilePath = Path.GetFullPath(Path.Combine(destinationDir, entryKey));

            // 3. Reject paths that resolve outside the destination directory
            if (!targetFilePath.StartsWith(destinationDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Potential Zip-Slip attack detected: Archive entry '{entryKey}' extracts outside target directory."
                );
            }

            // Ensure parent directory of the file exists
            string? parentDir = Path.GetDirectoryName(targetFilePath);
            if (parentDir != null && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            // Extract the file
            entry.WriteToFile(targetFilePath, new ExtractionOptions
            {
                Overwrite = true,
                ExtractFullPath = true
            });
        }
    }
}
