using System;
using System.IO;
using System.Linq;
using Xunit;
using ManagerIV.Core;

namespace ManagerIV.Tests;

public class SaveProfileManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SaveProfileManager _manager;

    public SaveProfileManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GtaIVSaveProfileTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _manager = new SaveProfileManager(_tempDir);
    }

    [Fact]
    public void TestGetBaseProfileIds()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_tempDir, "975EF3C9")); // Valid
        Directory.CreateDirectory(Path.Combine(_tempDir, "A1B2C3D4")); // Valid
        Directory.CreateDirectory(Path.Combine(_tempDir, "975EF3C9_Story1")); // Invalid (contains underscore)
        Directory.CreateDirectory(Path.Combine(_tempDir, "Short")); // Invalid (short)
        Directory.CreateDirectory(Path.Combine(_tempDir, "TooLongFolder")); // Invalid (too long)

        // Act
        var bases = _manager.GetBaseProfileIds();

        // Assert
        Assert.Equal(2, bases.Count);
        Assert.Contains("975EF3C9", bases);
        Assert.Contains("A1B2C3D4", bases);
    }

    [Fact]
    public void TestGetSaveProfiles()
    {
        // Arrange
        string baseId = "975EF3C9";
        string activePath = Path.Combine(_tempDir, baseId);
        string story1Path = Path.Combine(_tempDir, $"{baseId}_Story1");
        string unnamedPath = Path.Combine(_tempDir, $"{baseId}_Unnamed");

        Directory.CreateDirectory(activePath);
        Directory.CreateDirectory(story1Path);
        Directory.CreateDirectory(unnamedPath);

        // Write friendly name for Story1
        File.WriteAllText(Path.Combine(story1Path, "manageriv_save_name.txt"), "Main Story Save");

        // Act
        var profiles = _manager.GetSaveProfiles(baseId);

        // Assert
        Assert.Equal(3, profiles.Count);

        var active = profiles.First(p => p.IsActive);
        Assert.Equal(baseId, active.FolderName);
        Assert.Equal("Active Save (Default)", active.DisplayName);

        var story1 = profiles.First(p => p.FolderName == $"{baseId}_Story1");
        Assert.Equal("Main Story Save", story1.DisplayName);
        Assert.False(story1.IsActive);

        var unnamed = profiles.First(p => p.FolderName == $"{baseId}_Unnamed");
        Assert.Equal("Unnamed", unnamed.DisplayName);
        Assert.False(unnamed.IsActive);
    }

    [Fact]
    public void TestActivateSaveProfile()
    {
        // Arrange
        string baseId = "975EF3C9";
        string activePath = Path.Combine(_tempDir, baseId);
        string targetPath = Path.Combine(_tempDir, $"{baseId}_Mission20");

        Directory.CreateDirectory(activePath);
        Directory.CreateDirectory(targetPath);

        File.WriteAllText(Path.Combine(activePath, "savefile.dat"), "active save contents");
        File.WriteAllText(Path.Combine(targetPath, "savefile.dat"), "target save contents");

        var targetProfile = new SaveProfile($"{baseId}_Mission20", "Mission 20 Save", false, targetPath);

        // Act
        _manager.ActivateSaveProfile(baseId, targetProfile, "OldActiveSave");

        // Assert
        // Active should now contain target content
        Assert.True(Directory.Exists(activePath));
        Assert.Equal("target save contents", File.ReadAllText(Path.Combine(activePath, "savefile.dat")));
        Assert.Equal("Mission 20 Save", File.ReadAllText(Path.Combine(activePath, "manageriv_save_name.txt")).Trim());

        // Old active should be renamed to inactive
        string expectedInactivePath = Path.Combine(_tempDir, $"{baseId}_OldActiveSave");
        Assert.True(Directory.Exists(expectedInactivePath));
        Assert.Equal("active save contents", File.ReadAllText(Path.Combine(expectedInactivePath, "savefile.dat")));
        Assert.Equal("OldActiveSave", File.ReadAllText(Path.Combine(expectedInactivePath, "manageriv_save_name.txt")).Trim());
    }

    [Fact]
    public void TestActivateSaveProfile_RetainsCreatedName()
    {
        // Arrange
        string baseId = "975EF3C9";
        string activePath = Path.Combine(_tempDir, baseId);
        string targetPath = Path.Combine(_tempDir, $"{baseId}_Mission20");

        Directory.CreateDirectory(activePath);
        Directory.CreateDirectory(targetPath);

        File.WriteAllText(Path.Combine(activePath, "savefile.dat"), "active save contents");
        File.WriteAllText(Path.Combine(activePath, "manageriv_save_name.txt"), "My Story Run");
        File.WriteAllText(Path.Combine(targetPath, "savefile.dat"), "target save contents");

        var targetProfile = new SaveProfile($"{baseId}_Mission20", "Mission 20 Save", false, targetPath);

        // Act
        _manager.ActivateSaveProfile(baseId, targetProfile, string.Empty);

        // Assert
        // Active should now contain target content
        Assert.True(Directory.Exists(activePath));
        Assert.Equal("target save contents", File.ReadAllText(Path.Combine(activePath, "savefile.dat")));
        Assert.Equal("Mission 20 Save", File.ReadAllText(Path.Combine(activePath, "manageriv_save_name.txt")).Trim());

        // Old active should be renamed using "My Story Run"
        string expectedInactivePath = Path.Combine(_tempDir, $"{baseId}_My_Story_Run");
        Assert.True(Directory.Exists(expectedInactivePath));
        Assert.Equal("active save contents", File.ReadAllText(Path.Combine(expectedInactivePath, "savefile.dat")));
        Assert.Equal("My Story Run", File.ReadAllText(Path.Combine(expectedInactivePath, "manageriv_save_name.txt")).Trim());
    }

    [Fact]
    public void TestCreateNewSaveProfile()
    {
        // Arrange
        string baseId = "975EF3C9";
        string activePath = Path.Combine(_tempDir, baseId);
        Directory.CreateDirectory(activePath);
        File.WriteAllText(Path.Combine(activePath, "savefile.dat"), "old active save contents");

        // Act
        _manager.CreateNewSaveProfile(baseId, "Fresh Start", "ArchivedActive");

        // Assert
        // 1. New active folder is created and is empty of game saves
        Assert.True(Directory.Exists(activePath));
        Assert.False(File.Exists(Path.Combine(activePath, "savefile.dat")));
        Assert.Equal("Fresh Start", File.ReadAllText(Path.Combine(activePath, "manageriv_save_name.txt")).Trim());

        // 2. Old active folder is renamed to ArchivedActive
        string archivedPath = Path.Combine(_tempDir, $"{baseId}_ArchivedActive");
        Assert.True(Directory.Exists(archivedPath));
        Assert.Equal("old active save contents", File.ReadAllText(Path.Combine(archivedPath, "savefile.dat")));
        Assert.Equal("ArchivedActive", File.ReadAllText(Path.Combine(archivedPath, "manageriv_save_name.txt")).Trim());
    }

    [Fact]
    public void TestRenameSaveProfile()
    {
        // Arrange
        string baseId = "975EF3C9";
        string inactivePath = Path.Combine(_tempDir, $"{baseId}_OldName");
        Directory.CreateDirectory(inactivePath);
        
        var profile = new SaveProfile($"{baseId}_OldName", "Old Friendly Name", false, inactivePath);

        // Act
        _manager.RenameSaveProfile(profile, "New Shiny Name");

        // Assert
        string expectedNewPath = Path.Combine(_tempDir, $"{baseId}_New_Shiny_Name");
        Assert.True(Directory.Exists(expectedNewPath));
        Assert.False(Directory.Exists(inactivePath));
        Assert.Equal("New Shiny Name", File.ReadAllText(Path.Combine(expectedNewPath, "manageriv_save_name.txt")).Trim());
    }

    [Fact]
    public void TestDeleteSaveProfile()
    {
        // Arrange
        string baseId = "975EF3C9";
        string inactivePath = Path.Combine(_tempDir, $"{baseId}_DeleteMe");
        Directory.CreateDirectory(inactivePath);
        var profile = new SaveProfile($"{baseId}_DeleteMe", "To Delete", false, inactivePath);

        // Act
        _manager.DeleteSaveProfile(profile);

        // Assert
        Assert.False(Directory.Exists(inactivePath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }
}
