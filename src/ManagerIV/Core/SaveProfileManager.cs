using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ManagerIV.Core;

public class SaveProfileManager
{
    private readonly string _defaultProfilesPath;

    public SaveProfileManager(string? customProfilesPath = null)
    {
        _defaultProfilesPath = string.IsNullOrWhiteSpace(customProfilesPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Rockstar Games\GTA IV\Profiles")
            : customProfilesPath;
    }

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

                saveProfiles.Add(new SaveProfile(folderName, displayName, isActive, dir));
            }
        }
        catch { }

        return saveProfiles.OrderByDescending(p => p.IsActive).ThenBy(p => p.DisplayName).ToList();
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
            string cleanRenameTo = string.IsNullOrWhiteSpace(renameActiveTo)
                ? $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}"
                : renameActiveTo.Replace(" ", "_").Trim();

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
            string activeMetadata = Path.Combine(activePath, "manageriv_save_name.txt");
            try
            {
                File.WriteAllText(activeMetadata, string.IsNullOrWhiteSpace(renameActiveTo) ? "Auto Backup" : renameActiveTo);
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
            string cleanRenameTo = string.IsNullOrWhiteSpace(renameActiveTo)
                ? $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}"
                : renameActiveTo.Replace(" ", "_").Trim();

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                cleanRenameTo = cleanRenameTo.Replace(c, '_');
            }

            string inactivePath = Path.Combine(_defaultProfilesPath, $"{baseProfileId}_{cleanRenameTo}");
            if (Directory.Exists(inactivePath))
            {
                inactivePath += "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            }

            string activeMetadata = Path.Combine(activePath, "manageriv_save_name.txt");
            try
            {
                File.WriteAllText(activeMetadata, string.IsNullOrWhiteSpace(renameActiveTo) ? "Auto Backup" : renameActiveTo);
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
