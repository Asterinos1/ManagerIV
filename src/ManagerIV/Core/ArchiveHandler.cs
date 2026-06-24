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

    public enum ModLayout
    {
        FusionOverloaderReady,
        LegacyLoose,
        RawImg,
        Hybrid,
        Unknown
    }

    /// <summary>
    /// Detects the layout format of the extracted mod files and restructures them
    /// to follow the expected Fusion Overloader setup.
    /// </summary>
    public void PromoteModRoot(string destinationDir)
    {
        string fullDestinationPath = Path.GetFullPath(destinationDir);
        string effectiveRoot = GetEffectiveRoot(fullDestinationPath);

        string? updatePath = null;
        bool hasLegacyRoots = false;
        var legacyRootPaths = new List<string>();

        if (Directory.Exists(effectiveRoot))
        {
            string[] topDirs = Directory.GetDirectories(effectiveRoot);
            foreach (var subDir in topDirs)
            {
                string name = Path.GetFileName(subDir).ToLowerInvariant();
                if (name == "update")
                {
                    updatePath = subDir;
                }
                else if (name == "pc" || name == "common" || name == "tlad" || name == "tbogt")
                {
                    hasLegacyRoots = true;
                    legacyRootPaths.Add(subDir);
                }
            }
        }

        var allFiles = Directory.Exists(effectiveRoot)
            ? Directory.GetFiles(effectiveRoot, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();

        var imgFiles = allFiles.Where(f => f.EndsWith(".img", StringComparison.OrdinalIgnoreCase)).ToList();
        bool hasImgFiles = imgFiles.Count > 0;

        ModLayout layout = ModLayout.Unknown;
        if (updatePath != null && (hasLegacyRoots || imgFiles.Any(f => !f.StartsWith(updatePath, StringComparison.OrdinalIgnoreCase))))
        {
            layout = ModLayout.Hybrid;
        }
        else if (updatePath != null)
        {
            layout = ModLayout.FusionOverloaderReady;
        }
        else if (hasLegacyRoots)
        {
            layout = ModLayout.LegacyLoose;
        }
        else if (hasImgFiles)
        {
            layout = ModLayout.RawImg;
        }

        string tempPath = Path.Combine(Path.GetDirectoryName(fullDestinationPath)!, "temp_promote_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempPath);

        try
        {
            if (layout == ModLayout.FusionOverloaderReady || layout == ModLayout.Hybrid)
            {
                // Move contents of the update/ folder to temp
                if (updatePath != null && Directory.Exists(updatePath))
                {
                    MoveDirectoryContents(updatePath, tempPath);
                }
            }
            else if (layout == ModLayout.LegacyLoose || layout == ModLayout.Unknown)
            {
                // Move contents of effectiveRoot to temp
                if (Directory.Exists(effectiveRoot))
                {
                    MoveDirectoryContents(effectiveRoot, tempPath);
                }
            }
            else if (layout == ModLayout.RawImg)
            {
                // Promote .img files to temp with sub-game layout
                foreach (var imgFile in imgFiles)
                {
                    string subgame = GetSubgameFolder(imgFile);
                    string targetSubDir = string.IsNullOrEmpty(subgame) ? tempPath : Path.Combine(tempPath, subgame);
                    Directory.CreateDirectory(targetSubDir);
                    string destFile = Path.Combine(targetSubDir, Path.GetFileName(imgFile));
                    File.Move(imgFile, destFile, overwrite: true);
                }

                // Also copy readmes/configs to temp root
                var textFiles = allFiles.Where(f => {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".txt" || ext == ".ini" || ext == ".md";
                }).ToList();

                foreach (var txtFile in textFiles)
                {
                    string destFile = Path.Combine(tempPath, Path.GetFileName(txtFile));
                    if (!File.Exists(destFile))
                    {
                        File.Copy(txtFile, destFile);
                    }
                }
            }

            // Clean up original destinationDir
            if (Directory.Exists(fullDestinationPath))
            {
                Directory.Delete(fullDestinationPath, true);
            }

            // Move tempPath contents to destinationDir
            Directory.Move(tempPath, fullDestinationPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to promote mod root with layout {layout}: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(tempPath))
            {
                try { Directory.Delete(tempPath, true); } catch { }
            }
        }
    }

    private string GetEffectiveRoot(string dir)
    {
        if (!Directory.Exists(dir)) return dir;

        string[] files = Directory.GetFiles(dir);
        string[] dirs = Directory.GetDirectories(dir);
        
        var significantFiles = files.Where(f => {
            string name = Path.GetFileName(f).ToLowerInvariant();
            return name != ".ds_store" && name != "desktop.ini";
        }).ToList();

        if (dirs.Length == 1 && significantFiles.Count == 0)
        {
            return GetEffectiveRoot(dirs[0]);
        }
        return dir;
    }

    private string GetSubgameFolder(string imgPath)
    {
        string name = Path.GetFileName(imgPath).ToLowerInvariant();
        string rel = imgPath.Replace('\\', '/').ToLowerInvariant();
        if (name.Contains("tlad") || name.Contains("tl_") || name.Contains("eflc") ||
            rel.Contains("/tlad/") || rel.Contains("/eflc/"))
        {
            return "TLAD";
        }
        if (name.Contains("tbogt") || name.Contains("tg_") || name.Contains("gay_tony") ||
            rel.Contains("/tbogt/") || rel.Contains("/gay_tony/"))
        {
            return "TBoGT";
        }
        return "";
    }

    private void MoveDirectoryContents(string sourceDir, string targetDir)
    {
        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
            if (Directory.Exists(targetSubDir))
            {
                MoveDirectoryContents(dir, targetSubDir);
            }
            else
            {
                Directory.Move(dir, targetSubDir);
            }
        }

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Move(file, targetFile, overwrite: true);
        }
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
