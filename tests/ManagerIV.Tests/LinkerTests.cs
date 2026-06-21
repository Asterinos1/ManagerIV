using System;
using System.IO;
using Xunit;
using ManagerIV.Core;

namespace ManagerIV.Tests;

public class LinkerTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly string _libraryDir;
    private readonly string _gameDir;
    private readonly string _backupDir;
    private readonly NativeFileSystemLinker _linker;
    private readonly BackupRollbackService _rollbackService;

    public LinkerTests()
    {
        // Setup temporary test directories
        _testBaseDir = Path.Combine(Path.GetTempPath(), "ManagerIVTests_" + Guid.NewGuid().ToString("N"));
        _libraryDir = Path.Combine(_testBaseDir, "Library");
        _gameDir = Path.Combine(_testBaseDir, "Game");
        _backupDir = Path.Combine(_testBaseDir, "Backup");

        Directory.CreateDirectory(_libraryDir);
        Directory.CreateDirectory(_gameDir);
        Directory.CreateDirectory(_backupDir);

        _linker = new NativeFileSystemLinker();
        _rollbackService = new BackupRollbackService(_linker, _backupDir);
    }

    [Fact]
    public void TestJunctionCreationAndRollback()
    {
        // Arrange: Create a dummy mod folder in library with a test file
        string modName = "TestMod";
        string modSourcePath = Path.Combine(_libraryDir, modName);
        Directory.CreateDirectory(modSourcePath);

        string testFileName = "config.ini";
        string testFileSourcePath = Path.Combine(modSourcePath, testFileName);
        File.WriteAllText(testFileSourcePath, "Setting=Enabled");

        string junctionPath = Path.Combine(_gameDir, "update", "001_" + modName);
        var journal = new TransactionJournal();

        // Act: Deploy (create junction)
        _rollbackService.ExecuteCreateJunction(journal, junctionPath, modSourcePath);

        // Assert: Verify junction is created and points to target
        Assert.True(_linker.DirectoryExists(junctionPath), "Junction directory should exist.");
        Assert.True(_linker.IsJunction(junctionPath), "Directory should be recognized as a junction reparse point.");
        
        string linkedFilePath = Path.Combine(junctionPath, testFileName);
        Assert.True(_linker.FileExists(linkedFilePath), "Linked file should exist through the junction.");
        Assert.Equal("Setting=Enabled", File.ReadAllText(linkedFilePath));

        // Act: Rollback
        _rollbackService.Rollback(journal);

        // Assert: Verify junction is removed but original mod files remain untouched
        Assert.False(_linker.DirectoryExists(junctionPath), "Junction should be deleted after rollback.");
        Assert.True(_linker.DirectoryExists(modSourcePath), "Source library directory must remain intact.");
        Assert.True(_linker.FileExists(testFileSourcePath), "Source library file must remain intact.");
    }

    [Fact]
    public void TestHardLinkCreationAndRollback()
    {
        // Arrange: Create a dummy plugin file in library
        string modSourcePath = Path.Combine(_libraryDir, "PluginsMod");
        Directory.CreateDirectory(modSourcePath);

        string pluginFileName = "test_plugin.asi";
        string pluginSourcePath = Path.Combine(modSourcePath, pluginFileName);
        File.WriteAllText(pluginSourcePath, "[PluginData]");

        string linkPath = Path.Combine(_gameDir, "plugins", pluginFileName);
        var journal = new TransactionJournal();

        // Act: Deploy (create hard link)
        _rollbackService.ExecuteCreateHardLink(journal, linkPath, pluginSourcePath);

        // Assert: Verify hardlink is created
        Assert.True(_linker.FileExists(linkPath), "Hardlinked file should exist.");
        Assert.Equal("[PluginData]", File.ReadAllText(linkPath));

        // Act: Rollback
        _rollbackService.Rollback(journal);

        // Assert: Verify link is removed but source file remains intact
        Assert.False(_linker.FileExists(linkPath), "Hardlink should be deleted after rollback.");
        Assert.True(_linker.FileExists(pluginSourcePath), "Source plugin file must remain intact.");
    }

    [Fact]
    public void TestBackupAndReplaceFileRollback()
    {
        // Arrange: Create a file that already exists in the game directory (e.g. dinput8.dll)
        string existingGameFilePath = Path.Combine(_gameDir, "dinput8.dll");
        File.WriteAllText(existingGameFilePath, "OriginalDllContent");

        // Create a new source file that we want to replace it with
        string newSourceFilePath = Path.Combine(_libraryDir, "dinput8.dll");
        File.WriteAllText(newSourceFilePath, "NewDllContent");

        var journal = new TransactionJournal();

        // Act: Execute backup and replace
        _rollbackService.ExecuteBackupAndReplaceFile(journal, existingGameFilePath, newSourceFilePath);

        // Assert: Verify the file was replaced
        Assert.True(_linker.FileExists(existingGameFilePath));
        Assert.Equal("NewDllContent", File.ReadAllText(existingGameFilePath));

        // Act: Rollback
        _rollbackService.Rollback(journal);

        // Assert: Verify the original file content is restored
        Assert.True(_linker.FileExists(existingGameFilePath));
        Assert.Equal("OriginalDllContent", File.ReadAllText(existingGameFilePath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                // Delete everything safely
                Directory.Delete(_testBaseDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup exceptions in tests
        }
    }
}
