using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using GtaIVModLoader.Core;

namespace GtaIVModLoader.Tests;

public class StateAndConflictTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProfileManager _profileManager;
    private readonly LoadOrderService _loadOrderService;
    private readonly ConflictDetector _conflictDetector;

    public StateAndConflictTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GtaIVStateTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _profileManager = new ProfileManager();
        _loadOrderService = new LoadOrderService();
        _conflictDetector = new ConflictDetector();
    }

    [Fact]
    public void TestConflictDetectionAndLoadOrderWinner()
    {
        // Arrange: Create two mock mods both containing "data/handling.dat"
        var fileA = new ModFile("data/handling.dat", 1024, "hashA");
        var modA = new StagedMod(
            Id: "mod_a",
            Name: "Handling Fix A",
            Version: "1.0",
            Description: "Mod A description",
            LibraryPath: @"C:\Mods\mod_a",
            Files: new[] { fileA },
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        var fileB = new ModFile("data/handling.dat", 2048, "hashB");
        var modB = new StagedMod(
            Id: "mod_b",
            Name: "Handling Fix B",
            Version: "2.0",
            Description: "Mod B description",
            LibraryPath: @"C:\Mods\mod_b",
            Files: new[] { fileB },
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        var mods = new[] { modA, modB };

        // Act: Initialize load order (modA gets Priority 1, modB gets Priority 2)
        var loadOrder = _loadOrderService.InitializeLoadOrder(mods);
        
        // Assert initial order
        Assert.Equal(2, loadOrder.Entries.Count);
        Assert.Equal("mod_a", loadOrder.Entries[0].ModId);
        Assert.Equal(1, loadOrder.Entries[0].Priority);
        Assert.Equal("mod_b", loadOrder.Entries[1].ModId);
        Assert.Equal(2, loadOrder.Entries[1].Priority);

        // Act: Detect Conflicts (modB has higher priority and should win)
        var conflictState = _conflictDetector.DetectConflicts(mods, loadOrder);

        // Assert: Overlap detected and modB wins
        string expectedVirtualPath = "update/data/handling.dat";
        Assert.True(conflictState.Conflicts.ContainsKey(expectedVirtualPath), "Conflict should be registered for handling.dat.");
        
        var conflictInfo = conflictState.Conflicts[expectedVirtualPath];
        Assert.Equal("mod_b", conflictInfo.WinnerModId);
        Assert.Contains("mod_a", conflictInfo.ConflictingModIds);

        // Verify warning generated for handling.dat
        Assert.Single(conflictState.Warnings);
        Assert.Contains("Conflict on handling configuration file", conflictState.Warnings[0]);

        // Act: Reorder mods so modA overrides modB (modA moves to priority 2)
        var updatedLoadOrder = _loadOrderService.ReorderMod(loadOrder, "mod_a", 2);

        // Assert priorities were updated
        Assert.Equal("mod_b", updatedLoadOrder.Entries[0].ModId);
        Assert.Equal(1, updatedLoadOrder.Entries[0].Priority);
        Assert.Equal("mod_a", updatedLoadOrder.Entries[1].ModId);
        Assert.Equal(2, updatedLoadOrder.Entries[1].Priority);

        // Act: Re-run conflict detection with new order
        var updatedConflictState = _conflictDetector.DetectConflicts(mods, updatedLoadOrder);

        // Assert: Overlap detected and modA now wins
        var updatedConflictInfo = updatedConflictState.Conflicts[expectedVirtualPath];
        Assert.Equal("mod_a", updatedConflictInfo.WinnerModId);
        Assert.Contains("mod_b", updatedConflictInfo.ConflictingModIds);
    }

    [Fact]
    public void TestProfileSaveAndLoad()
    {
        // Arrange: Create a mock profile
        var loadOrder = new LoadOrderModel(new[]
        {
            new LoadOrderEntry("mod_1", DeployTarget.Update, 1),
            new LoadOrderEntry("mod_2", DeployTarget.Plugins, 2)
        });

        var conflicts = new Dictionary<string, ConflictInfo>
        {
            { "update/data/handling.dat", new ConflictInfo("update/data/handling.dat", "mod_2", new[] { "mod_1" }) }
        };
        var warnings = new[] { "Warning description" };
        var conflictState = new ConflictState(conflicts, warnings);

        var profile = new Profile(
            Id: "profile_test",
            Name: "My High Priority Mods",
            GamePath: @"C:\Games\GTAIV",
            LibraryPath: @"C:\Users\User\GtaIVMods",
            EnabledModIds: new[] { "mod_1", "mod_2" },
            LoadOrder: loadOrder,
            ConflictState: conflictState
        );

        string filePath = Path.Combine(_tempDir, "test_profile.json");

        // Act: Save profile
        _profileManager.SaveProfile(filePath, profile);

        // Act: Load profile
        Assert.True(File.Exists(filePath), "JSON profile file should exist on disk.");
        var loadedProfile = _profileManager.LoadProfile(filePath);

        // Assert: Loaded matches original
        Assert.Equal(profile.Id, loadedProfile.Id);
        Assert.Equal(profile.Name, loadedProfile.Name);
        Assert.Equal(profile.GamePath, loadedProfile.GamePath);
        Assert.Equal(profile.LibraryPath, loadedProfile.LibraryPath);
        Assert.Equal(profile.EnabledModIds, loadedProfile.EnabledModIds);
        Assert.Equal(profile.LoadOrder.Entries.Count, loadedProfile.LoadOrder.Entries.Count);
        
        Assert.Equal(profile.LoadOrder.Entries[0].ModId, loadedProfile.LoadOrder.Entries[0].ModId);
        Assert.Equal(profile.LoadOrder.Entries[0].Target, loadedProfile.LoadOrder.Entries[0].Target);
        Assert.Equal(profile.LoadOrder.Entries[0].Priority, loadedProfile.LoadOrder.Entries[0].Priority);

        Assert.Equal(profile.ConflictState.Conflicts.Count, loadedProfile.ConflictState.Conflicts.Count);
        var originalConflict = profile.ConflictState.Conflicts["update/data/handling.dat"];
        var loadedConflict = loadedProfile.ConflictState.Conflicts["update/data/handling.dat"];
        Assert.Equal(originalConflict.WinnerModId, loadedConflict.WinnerModId);
        Assert.Equal(originalConflict.ConflictingModIds, loadedConflict.ConflictingModIds);
        Assert.Equal(profile.ConflictState.Warnings, loadedProfile.ConflictState.Warnings);
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
        catch
        {
            // Ignore cleanup errors
        }
    }
}
