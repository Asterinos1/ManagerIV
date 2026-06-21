using System;
using System.IO;
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
