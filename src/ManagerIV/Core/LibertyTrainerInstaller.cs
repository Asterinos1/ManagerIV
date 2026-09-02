using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace ManagerIV.Core;

/// <summary>
/// Implements installation, rollback, conflict detection, and uninstallation for Liberty's Legacy.
/// </summary>
public class LibertyTrainerInstaller : ILibertyTrainerInstaller
{
    private static readonly string[] KnownConflictingTrainerFiles = new[]
    {
        "SimpleTrainer.asi",
        "Trainer.asi",
        "NativeTrainer.asi",
        "ZMenu.asi",
        "zolika1351s_trainer.asi",
        "TrainerIV.asi",
        "GTAIV_Trainer.asi",
        "infin_trainer.asi",
        "trainer_ce.asi",
        "GTAIVMenu.asi",
        "SNT.asi",
        "Trainer.ini",
        "SimpleTrainer.ini"
    };

    private static readonly string[] ConfigExtensions = new[]
    {
        ".ini", ".cfg", ".json", ".xml", ".txt", ".dat", ".sav"
    };

    private readonly ILibertyTrainerValidator _validator;

    public LibertyTrainerInstaller(ILibertyTrainerValidator validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <inheritdoc />
    public TrainerStatus GetTrainerStatus(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            return TrainerStatus.Missing;
        }

        string asiPath = Path.Combine(gamePath, "Liberty's Legacy.asi");
        string dirPath = Path.Combine(gamePath, "Liberty's Legacy");

        bool asiExists = File.Exists(asiPath);
        bool dirExists = Directory.Exists(dirPath) && Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories).Length > 0;

        if (asiExists && dirExists)
        {
            return TrainerStatus.Installed;
        }

        if (asiExists || Directory.Exists(dirPath))
        {
            return TrainerStatus.RepairNeeded;
        }

        return TrainerStatus.Missing;
    }

    /// <inheritdoc />
    public string GetInstalledVersion(string gamePath, Profile? profile)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            return "";
        }

        string asiPath = Path.Combine(gamePath, "Liberty's Legacy.asi");
        if (File.Exists(asiPath))
        {
            try
            {
                var fileVersionInfo = FileVersionInfo.GetVersionInfo(asiPath);
                string? ver = fileVersionInfo.FileVersion;
                if (!string.IsNullOrWhiteSpace(ver) && ver != "0.0.0.0")
                {
                    return $"v{ver.TrimStart('v', 'V')}";
                }

                string? prodVer = fileVersionInfo.ProductVersion;
                if (!string.IsNullOrWhiteSpace(prodVer) && prodVer != "0.0.0.0")
                {
                    return $"v{prodVer.TrimStart('v', 'V')}";
                }
            }
            catch { }
        }

        if (profile != null && profile.ToolVersions.TryGetValue("LibertysLegacy", out string? recordedVer) && !string.IsNullOrWhiteSpace(recordedVer))
        {
            return recordedVer;
        }

        var status = GetTrainerStatus(gamePath);
        if (status == TrainerStatus.Installed)
        {
            return "Unknown / Manual installation";
        }

        return "";
    }

    /// <inheritdoc />
    public TrainerConflictInfo DetectConflicts(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            return new TrainerConflictInfo(false, Array.Empty<string>());
        }

        var conflictingFiles = new List<string>();

        // Check root game directory
        CheckDirectoryForConflicts(gamePath, conflictingFiles);

        // Check plugins directory
        string pluginsDir = Path.Combine(gamePath, "plugins");
        if (Directory.Exists(pluginsDir))
        {
            CheckDirectoryForConflicts(pluginsDir, conflictingFiles);
        }

        // Check scripts directory
        string scriptsDir = Path.Combine(gamePath, "scripts");
        if (Directory.Exists(scriptsDir))
        {
            CheckDirectoryForConflicts(scriptsDir, conflictingFiles);
        }

        return new TrainerConflictInfo(conflictingFiles.Count > 0, conflictingFiles);
    }

    private static void CheckDirectoryForConflicts(string directory, List<string> conflictingFiles)
    {
        try
        {
            var files = Directory.GetFiles(directory);
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                if (fileName.Equals("Liberty's Legacy.asi", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (KnownConflictingTrainerFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    conflictingFiles.Add(file);
                    continue;
                }

                if (fileName.EndsWith(".asi", StringComparison.OrdinalIgnoreCase))
                {
                    string lower = fileName.ToLowerInvariant();
                    if ((lower.Contains("trainer") || lower.Contains("zmenu") || lower.Contains("menu")) &&
                        !lower.Contains("fusionfix") && !lower.Contains("completeeditionhook"))
                    {
                        conflictingFiles.Add(file);
                    }
                }
            }
        }
        catch { }
    }

    /// <inheritdoc />
    public async Task<string> InstallTrainerAsync(
        string archivePath,
        string gamePath,
        Profile profile,
        List<InstalledToolFile> manifest,
        IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            throw new DirectoryNotFoundException($"Game directory not found: '{gamePath}'");
        }

        progress?.Report("Validating archive structure...");
        var validation = _validator.ValidateArchive(archivePath);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(validation.ErrorMessage ?? "Archive validation failed.");
        }

        string tempExtractDir = Path.Combine(Path.GetTempPath(), "ManagerIV_LL_Extract_" + Guid.NewGuid().ToString("N"));
        string tempBackupDir = Path.Combine(Path.GetTempPath(), "ManagerIV_LL_Backup_" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempExtractDir);

        bool backupCreated = false;
        var deployedFiles = new List<string>();

        try
        {
            progress?.Report("Extracting package to temporary staging area...");
            using (var stream = File.OpenRead(archivePath))
            using (var archive = ArchiveFactory.OpenArchive(stream))
            {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    if (string.IsNullOrWhiteSpace(entry.Key)) continue;
                    string entryKey = entry.Key.Replace('\\', '/').TrimStart('/');
                    string targetFilePath = Path.Combine(tempExtractDir, entryKey);
                    string? dir = Path.GetDirectoryName(targetFilePath);
                    if (dir != null && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    entry.WriteToFile(targetFilePath, new ExtractionOptions { Overwrite = true, ExtractFullPath = true });
                }
            }

            string resolvedRoot = string.IsNullOrEmpty(validation.ResolvedRootPrefix)
                ? tempExtractDir
                : Path.Combine(tempExtractDir, validation.ResolvedRootPrefix.Trim('/'));

            string sourceAsi = Path.Combine(resolvedRoot, "Liberty's Legacy.asi");
            string sourceCompanionDir = Path.Combine(resolvedRoot, "Liberty's Legacy");

            if (!File.Exists(sourceAsi))
            {
                // Fallback search in extracted folder
                sourceAsi = Directory.GetFiles(tempExtractDir, "Liberty's Legacy.asi", SearchOption.AllDirectories).FirstOrDefault()
                    ?? throw new FileNotFoundException("Could not find extracted 'Liberty's Legacy.asi'.");
            }

            if (!Directory.Exists(sourceCompanionDir))
            {
                sourceCompanionDir = Directory.GetDirectories(tempExtractDir, "Liberty's Legacy", SearchOption.AllDirectories).FirstOrDefault()
                    ?? throw new DirectoryNotFoundException("Could not find extracted 'Liberty's Legacy' companion directory.");
            }

            // Create pre-deployment backup for rollback
            progress?.Report("Creating restore point of existing trainer files...");
            string targetAsi = Path.Combine(gamePath, "Liberty's Legacy.asi");
            string targetCompanionDir = Path.Combine(gamePath, "Liberty's Legacy");

            if (File.Exists(targetAsi))
            {
                Directory.CreateDirectory(tempBackupDir);
                File.Copy(targetAsi, Path.Combine(tempBackupDir, "Liberty's Legacy.asi"), overwrite: true);
                backupCreated = true;
            }

            if (Directory.Exists(targetCompanionDir))
            {
                Directory.CreateDirectory(tempBackupDir);
                CopyDirectoryRecursive(targetCompanionDir, Path.Combine(tempBackupDir, "Liberty's Legacy"));
                backupCreated = true;
            }

            // Load existing manifest records for LibertysLegacy
            var oldManifestMap = manifest
                .Where(f => string.Equals(f.SourceTool, "LibertysLegacy", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(f => f.InstalledPath, f => f.Sha256, StringComparer.OrdinalIgnoreCase);

            progress?.Report("Deploying Liberty's Legacy to game root...");

            // Deploy ASI
            File.Copy(sourceAsi, targetAsi, overwrite: true);
            deployedFiles.Add(targetAsi);

            // Deploy Companion Folder
            Directory.CreateDirectory(targetCompanionDir);
            var sourceFiles = Directory.GetFiles(sourceCompanionDir, "*", SearchOption.AllDirectories);

            foreach (var sourceFile in sourceFiles)
            {
                string relPath = Path.GetRelativePath(sourceCompanionDir, sourceFile);
                string destFile = Path.Combine(targetCompanionDir, relPath);
                string? destDir = Path.GetDirectoryName(destFile);
                if (destDir != null && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                bool shouldPreserve = false;
                if (File.Exists(destFile))
                {
                    string ext = Path.GetExtension(destFile).ToLowerInvariant();
                    string currentHash = await ComputeFileHashAsync(destFile);

                    if (oldManifestMap.TryGetValue(destFile, out string? recordedHash))
                    {
                        // If file modified by user, preserve user copy
                        if (!string.Equals(currentHash, recordedHash, StringComparison.OrdinalIgnoreCase))
                        {
                            shouldPreserve = true;
                        }
                    }
                    else if (ConfigExtensions.Contains(ext))
                    {
                        // Unrecorded existing config/data file: preserve it
                        shouldPreserve = true;
                    }
                }

                if (!shouldPreserve)
                {
                    File.Copy(sourceFile, destFile, overwrite: true);
                }

                deployedFiles.Add(destFile);
            }

            // Update Manifest
            progress?.Report("Recording installed files in manifest...");
            manifest.RemoveAll(f => string.Equals(f.SourceTool, "LibertysLegacy", StringComparison.OrdinalIgnoreCase));

            foreach (var file in deployedFiles)
            {
                if (File.Exists(file))
                {
                    string hash = await ComputeFileHashAsync(file);
                    manifest.Add(new InstalledToolFile("LibertysLegacy", file, hash));
                }
            }

            // Version determination
            string version = validation.DetectedVersion ?? "v2.4.1";
            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(targetAsi);
                if (!string.IsNullOrWhiteSpace(fvi.FileVersion) && fvi.FileVersion != "0.0.0.0")
                {
                    version = $"v{fvi.FileVersion.TrimStart('v', 'V')}";
                }
            }
            catch { }

            progress?.Report($"Liberty's Legacy {version} installed successfully!");
            return version;
        }
        catch (Exception ex)
        {
            progress?.Report($"Deployment failed: {ex.Message}. Rolling back...");

            // Rollback newly copied files
            foreach (var file in deployedFiles)
            {
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                }
                catch { }
            }

            // Restore from backup
            if (backupCreated && Directory.Exists(tempBackupDir))
            {
                string backupAsi = Path.Combine(tempBackupDir, "Liberty's Legacy.asi");
                string backupDir = Path.Combine(tempBackupDir, "Liberty's Legacy");

                if (File.Exists(backupAsi))
                {
                    try { File.Copy(backupAsi, Path.Combine(gamePath, "Liberty's Legacy.asi"), overwrite: true); } catch { }
                }

                if (Directory.Exists(backupDir))
                {
                    try { CopyDirectoryRecursive(backupDir, Path.Combine(gamePath, "Liberty's Legacy")); } catch { }
                }
            }

            throw new InvalidOperationException($"Liberty's Legacy installation failed and was rolled back: {ex.Message}", ex);
        }
        finally
        {
            // Cleanup temp directories
            try
            {
                if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
                if (Directory.Exists(tempBackupDir)) Directory.Delete(tempBackupDir, true);
            }
            catch { }
        }
    }

    /// <inheritdoc />
    public async Task UninstallTrainerAsync(
        string gamePath,
        List<InstalledToolFile> manifest,
        bool preserveUserData = true)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            return;
        }

        var trainerFiles = manifest
            .Where(f => string.Equals(f.SourceTool, "LibertysLegacy", StringComparison.OrdinalIgnoreCase))
            .ToList();

        string asiPath = Path.Combine(gamePath, "Liberty's Legacy.asi");
        string companionDir = Path.Combine(gamePath, "Liberty's Legacy");

        if (File.Exists(asiPath))
        {
            try { File.Delete(asiPath); } catch { }
        }

        foreach (var item in trainerFiles)
        {
            if (item.InstalledPath.Equals(asiPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!File.Exists(item.InstalledPath)) continue;

            if (preserveUserData)
            {
                string ext = Path.GetExtension(item.InstalledPath).ToLowerInvariant();
                string currentHash = await ComputeFileHashAsync(item.InstalledPath);

                // Preserve if user modified it or if it is a configuration/save file
                if (!string.Equals(currentHash, item.Sha256, StringComparison.OrdinalIgnoreCase) ||
                    ConfigExtensions.Contains(ext))
                {
                    continue;
                }
            }

            try { File.Delete(item.InstalledPath); } catch { }
        }

        if (!preserveUserData && Directory.Exists(companionDir))
        {
            try { Directory.Delete(companionDir, recursive: true); } catch { }
        }
        else if (Directory.Exists(companionDir))
        {
            // If companion directory is now empty, clean it up; if it has preserved user files, leave it intact.
            DeleteDirectoryIfEmpty(companionDir);
        }

        // Clean manifest
        manifest.RemoveAll(f => string.Equals(f.SourceTool, "LibertysLegacy", StringComparison.OrdinalIgnoreCase));
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            string destSub = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, destSub);
        }
    }

    private static void DeleteDirectoryIfEmpty(string dirPath)
    {
        try
        {
            foreach (var sub in Directory.GetDirectories(dirPath))
            {
                DeleteDirectoryIfEmpty(sub);
            }

            if (Directory.GetFiles(dirPath).Length == 0 && Directory.GetDirectories(dirPath).Length == 0)
            {
                Directory.Delete(dirPath);
            }
        }
        catch { }
    }

    private static async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        byte[] hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
