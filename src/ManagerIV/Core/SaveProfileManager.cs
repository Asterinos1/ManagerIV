using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ManagerIV.Core;

/// <summary>
/// Service responsible for managing GTA IV save game profiles, handles detecting active profiles, creating backups/slots, and switching profiles.
/// </summary>
public class SaveProfileManager
{
    private readonly string _defaultProfilesPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveProfileManager"/> class.
    /// </summary>
    /// <param name="customProfilesPath">An optional custom folder path where save profiles are stored.</param>
    public SaveProfileManager(string? customProfilesPath = null)
    {
        _defaultProfilesPath = string.IsNullOrWhiteSpace(customProfilesPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Rockstar Games\GTA IV\Profiles")
            : customProfilesPath;
    }

    /// <summary>
    /// Gets the folder path where the save game profiles are located.
    /// </summary>
    public string ProfilesPath => _defaultProfilesPath;

    /// <summary>
    /// Gets all base profile IDs (8-character hex or alphanumeric directories) from the GTA IV Profiles folder.
    /// </summary>
    public List<string> GetBaseProfileIds()
    {
        if (!Directory.Exists(_defaultProfilesPath))
        {
            return new List<string>();
        }

        try
        {
            return Directory.GetDirectories(_defaultProfilesPath)
                .Select(Path.GetFileName)
                .Where(name => name != null && name.Length == 8 && name.All(char.IsLetterOrDigit))
                .ToList()!;
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Gets all save profiles (both active and inactive) for a specific base profile ID.
    /// </summary>
    public List<SaveProfile> GetSaveProfiles(string baseProfileId)
    {
        if (string.IsNullOrWhiteSpace(baseProfileId) || !Directory.Exists(_defaultProfilesPath))
        {
            return new List<SaveProfile>();
        }

        var saveProfiles = new List<SaveProfile>();
        try
        {
            var directories = Directory.GetDirectories(_defaultProfilesPath, $"{baseProfileId}*");
            foreach (var dir in directories)
            {
                string folderName = Path.GetFileName(dir);
                bool isActive = folderName.Equals(baseProfileId, StringComparison.OrdinalIgnoreCase);
                
                // Read display name from metadata file inside directory if it exists
                string displayName = "";
                string metadataFile = Path.Combine(dir, "manageriv_save_name.txt");
                if (File.Exists(metadataFile))
                {
                    try
                    {
                        displayName = File.ReadAllText(metadataFile).Trim();
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    if (isActive)
                    {
                        displayName = "Active Save (Default)";
                    }
                    else
                    {
                        // Suffix is after underscore
                        int underscoreIndex = folderName.IndexOf('_');
                        if (underscoreIndex >= 0 && underscoreIndex < folderName.Length - 1)
                        {
                            displayName = folderName.Substring(underscoreIndex + 1);
                        }
                        else
                        {
                            displayName = folderName;
                        }
                    }
                }

                int fileCount = 0;
                DateTime? lastModified = null;
                long totalSizeBytes = 0;

                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                    fileCount = files.Count(f => f.Name.StartsWith("SGTA", StringComparison.OrdinalIgnoreCase));
                    if (fileCount == 0) fileCount = files.Length;

                    if (files.Length > 0)
                    {
                        lastModified = files.Max(f => f.LastWriteTime);
                        totalSizeBytes = files.Sum(f => f.Length);
                    }
                    else
                    {
                        lastModified = dirInfo.LastWriteTime;
                    }
                }
                catch { }

                saveProfiles.Add(new SaveProfile(folderName, displayName, isActive, dir, fileCount, lastModified, totalSizeBytes));
            }
        }
        catch { }

        return saveProfiles.OrderByDescending(p => p.IsActive).ThenBy(p => p.DisplayName).ToList();
    }

    /// <summary>
    /// Creates an instant backup clone/snapshot of the active save profile directory without deactivating it.
    /// </summary>
    public SaveProfile CloneActiveSaveProfile(string baseProfileId, string snapshotDisplayName)
    {
        if (string.IsNullOrWhiteSpace(baseProfileId) || !Directory.Exists(_defaultProfilesPath))
            throw new ArgumentException("Base profile ID must be valid and profile directory must exist.");

        string activePath = Path.Combine(_defaultProfilesPath, baseProfileId);
        if (!Directory.Exists(activePath))
            throw new DirectoryNotFoundException($"Active save directory '{activePath}' not found.");

        string cleanName = string.IsNullOrWhiteSpace(snapshotDisplayName)
            ? $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}"
            : snapshotDisplayName.Replace(" ", "_").Trim();

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            cleanName = cleanName.Replace(c, '_');
        }

        string targetPath = Path.Combine(_defaultProfilesPath, $"{baseProfileId}_{cleanName}");
        if (Directory.Exists(targetPath))
        {
            targetPath += "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        CopyDirectory(activePath, targetPath);

        string metadataName = !string.IsNullOrWhiteSpace(snapshotDisplayName)
            ? snapshotDisplayName
            : $"Snapshot {DateTime.Now:g}";

        try
        {
            File.WriteAllText(Path.Combine(targetPath, "manageriv_save_name.txt"), metadataName);
        }
        catch { }

        return new SaveProfile(
            Path.GetFileName(targetPath),
            metadataName,
            false,
            targetPath
        );
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    /// <summary>
    /// Activates a specific save profile.
    /// </summary>
    public void ActivateSaveProfile(string baseProfileId, SaveProfile targetProfile, string renameActiveTo)
    {
        if (string.IsNullOrWhiteSpace(baseProfileId) || targetProfile == null)
            throw new ArgumentNullException(nameof(targetProfile));

        string activePath = Path.Combine(_defaultProfilesPath, baseProfileId);
        
        // 1. Deactivate current active profile if it exists
        if (Directory.Exists(activePath))
        {
            string? existingName = null;
            string activeMetadata = Path.Combine(activePath, "manageriv_save_name.txt");
            if (File.Exists(activeMetadata))
            {
                try
                {
                    existingName = File.ReadAllText(activeMetadata).Trim();
                }
                catch { }
            }

            string cleanRenameTo;
            if (!string.IsNullOrWhiteSpace(renameActiveTo))
            {
                cleanRenameTo = renameActiveTo.Replace(" ", "_").Trim();
            }
            else if (!string.IsNullOrWhiteSpace(existingName) && !existingName.Equals("Active Save (Default)", StringComparison.OrdinalIgnoreCase))
            {
                cleanRenameTo = existingName.Replace(" ", "_").Trim();
            }
            else
            {
                cleanRenameTo = $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            // Sanitize file/folder name
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                cleanRenameTo = cleanRenameTo.Replace(c, '_');
            }

            string inactivePath = Path.Combine(_defaultProfilesPath, $"{baseProfileId}_{cleanRenameTo}");
            
            // If target folder already exists, append unique suffix
            if (Directory.Exists(inactivePath))
            {
                inactivePath += "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            }

            // Write metadata name before renaming
            try
            {
                string metadataName = !string.IsNullOrWhiteSpace(renameActiveTo)
                    ? renameActiveTo
                    : (!string.IsNullOrWhiteSpace(existingName) ? existingName : "Auto Backup");
                File.WriteAllText(activeMetadata, metadataName);
            }
            catch { }

            Directory.Move(activePath, inactivePath);
        }

        // 2. Activate target profile
        if (Directory.Exists(targetProfile.FullPath))
        {
            // Write display name to target before move
            string targetMetadata = Path.Combine(targetProfile.FullPath, "manageriv_save_name.txt");
            try
            {
                File.WriteAllText(targetMetadata, targetProfile.DisplayName);
            }
            catch { }

            Directory.Move(targetProfile.FullPath, activePath);
        }
        else
        {
            throw new DirectoryNotFoundException($"Target save profile directory '{targetProfile.FullPath}' not found.");
        }
    }

    /// <summary>
    /// Creates a new save profile (fresh story).
    /// </summary>
    public void CreateNewSaveProfile(string baseProfileId, string newProfileName, string renameActiveTo)
    {
        if (string.IsNullOrWhiteSpace(baseProfileId) || string.IsNullOrWhiteSpace(newProfileName))
            throw new ArgumentException("Profile ID and new profile name must be provided.");

        string activePath = Path.Combine(_defaultProfilesPath, baseProfileId);

        // 1. Deactivate current active profile if it exists
        if (Directory.Exists(activePath))
        {
            string? existingName = null;
            string activeMetadata = Path.Combine(activePath, "manageriv_save_name.txt");
            if (File.Exists(activeMetadata))
            {
                try
                {
                    existingName = File.ReadAllText(activeMetadata).Trim();
                }
                catch { }
            }

            string cleanRenameTo;
            if (!string.IsNullOrWhiteSpace(renameActiveTo))
            {
                cleanRenameTo = renameActiveTo.Replace(" ", "_").Trim();
            }
            else if (!string.IsNullOrWhiteSpace(existingName) && !existingName.Equals("Active Save (Default)", StringComparison.OrdinalIgnoreCase))
            {
                cleanRenameTo = existingName.Replace(" ", "_").Trim();
            }
            else
            {
                cleanRenameTo = $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                cleanRenameTo = cleanRenameTo.Replace(c, '_');
            }

            string inactivePath = Path.Combine(_defaultProfilesPath, $"{baseProfileId}_{cleanRenameTo}");
            if (Directory.Exists(inactivePath))
            {
                inactivePath += "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            }

            try
            {
                string metadataName = !string.IsNullOrWhiteSpace(renameActiveTo)
                    ? renameActiveTo
                    : (!string.IsNullOrWhiteSpace(existingName) ? existingName : "Auto Backup");
                File.WriteAllText(activeMetadata, metadataName);
            }
            catch { }

            Directory.Move(activePath, inactivePath);
        }

        // 2. Create a new empty active directory (GTA IV will populate it when started)
        Directory.CreateDirectory(activePath);
        string newMetadataFile = Path.Combine(activePath, "manageriv_save_name.txt");
        try
        {
            File.WriteAllText(newMetadataFile, newProfileName);
        }
        catch { }
    }

    /// <summary>
    /// Renames a save profile.
    /// </summary>
    public void RenameSaveProfile(SaveProfile saveProfile, string newName)
    {
        if (saveProfile == null || string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Save profile and new name must be valid.");

        if (!Directory.Exists(saveProfile.FullPath))
            throw new DirectoryNotFoundException($"Save profile directory '{saveProfile.FullPath}' not found.");

        // Write new name to metadata file
        string metadataFile = Path.Combine(saveProfile.FullPath, "manageriv_save_name.txt");
        try
        {
            File.WriteAllText(metadataFile, newName);
        }
        catch { }

        // If inactive, rename physical folder to match the new display name suffix
        if (!saveProfile.IsActive)
        {
            string cleanName = newName.Replace(" ", "_").Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                cleanName = cleanName.Replace(c, '_');
            }

            // extract base profile ID (first 8 characters)
            string baseId = saveProfile.FolderName.Substring(0, 8);
            string newPath = Path.Combine(_defaultProfilesPath, $"{baseId}_{cleanName}");
            
            if (newPath != saveProfile.FullPath)
            {
                if (Directory.Exists(newPath))
                {
                    newPath += "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                }
                Directory.Move(saveProfile.FullPath, newPath);
            }
        }
    }

    /// <summary>
    /// Deletes a save profile directory.
    /// </summary>
    public void DeleteSaveProfile(SaveProfile saveProfile)
    {
        if (saveProfile == null)
            throw new ArgumentNullException(nameof(saveProfile));

        if (Directory.Exists(saveProfile.FullPath))
        {
            Directory.Delete(saveProfile.FullPath, true);
        }
    }
}
