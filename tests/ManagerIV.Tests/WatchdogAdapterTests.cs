using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ManagerIV.Core;

namespace ManagerIV.Tests;

public class WatchdogAdapterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly UpdateWatchdog _watchdog;
    private readonly CompleteEditionAdapter _adapter;

    public WatchdogAdapterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GtaIVWatchdogTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _watchdog = new UpdateWatchdog();
        _adapter = new CompleteEditionAdapter(_tempDir, new NativeFileSystemLinker());
    }

    [Fact]
    public async Task TestUpdateWatchdogMismatch()
    {
        // Arrange: Create dummy GTAIV.exe
        string exePath = Path.Combine(_tempDir, "GTAIV.exe");
        await File.WriteAllTextAsync(exePath, "Version 1.0 Original Executable Payload Data");

        // Capture initial state
        var originalProfile = await _watchdog.CaptureCurrentVersionAsync(_tempDir);
        Assert.NotNull(originalProfile);
        Assert.True(originalProfile.ExecutableSize > 0);
        Assert.NotEmpty(originalProfile.ExecutableHash);

        // Build active Profile with this initial game version profile
        var activeProfile = new Profile(
            Id: "test_profile",
            Name: "Test Profile",
            GamePath: _tempDir,
            LibraryPath: _tempDir,
            EnabledModIds: Array.Empty<string>(),
            LoadOrder: new LoadOrderModel(Array.Empty<LoadOrderEntry>()),
            ConflictState: new ConflictState(new System.Collections.Generic.Dictionary<string, ConflictInfo>(), Array.Empty<string>()),
            LastKnownVersion: originalProfile
        );

        // Act: Verify (should be Match)
        var result1 = await _watchdog.VerifyGameVersionAsync(_tempDir, activeProfile);
        Assert.Equal(WatchdogStatus.Match, result1.Status);

        // Act: Modify the executable (change size and hash)
        await File.WriteAllTextAsync(exePath, "Version 1.1 Updated Executable Payload Data - Longer String");

        // Act: Verify again (should be Mismatch)
        var result2 = await _watchdog.VerifyGameVersionAsync(_tempDir, activeProfile);
        Assert.Equal(WatchdogStatus.Mismatch, result2.Status);
        Assert.Contains("GTAIV.exe changed", result2.Message);

        // Act: Delete the executable (should be MissingExecutable)
        File.Delete(exePath);
        var result3 = await _watchdog.VerifyGameVersionAsync(_tempDir, activeProfile);
        Assert.Equal(WatchdogStatus.MissingExecutable, result3.Status);
    }

    [Fact]
    public void TestCompleteEditionAdapterResolveTarget()
    {
        // Act & Assert: Verify .asi files go to Plugins target
        var asiFile = new ModFile("first_plugin.asi", 500, "hash1");
        Assert.Equal(DeployTarget.Plugins, _adapter.ResolveTarget(asiFile));

        var nestedAsiFile = new ModFile("plugins/NestedLoader.asi", 500, "hash2");
        Assert.Equal(DeployTarget.Plugins, _adapter.ResolveTarget(nestedAsiFile));

        // Act & Assert: Verify asset files go to Update target
        var assetFile = new ModFile("common/data/handling.dat", 1000, "hash3");
        Assert.Equal(DeployTarget.Update, _adapter.ResolveTarget(assetFile));

        var genericFile = new ModFile("modloader.ini", 200, "hash4");
        Assert.Equal(DeployTarget.Update, _adapter.ResolveTarget(genericFile));

        // Act & Assert: Verify dll/script files go to Scripts target
        var scriptDllFile = new ModFile("scripts/my_script.dll", 50000, "hash5");
        Assert.Equal(DeployTarget.Scripts, _adapter.ResolveTarget(scriptDllFile));

        var scriptFile = new ModFile("MyNetScript.dll", 50000, "hash6");
        Assert.Equal(DeployTarget.Scripts, _adapter.ResolveTarget(scriptFile));
    }

    [Fact]
    public async Task TestCompleteEditionAdapterUndeploy()
    {
        // Arrange
        var pluginMod = new StagedMod(
            Id: "plugin_mod",
            Name: "Plugin Mod",
            Version: "1.0",
            Description: "A test plugin mod",
            LibraryPath: Path.Combine(_tempDir, "Library", "plugin_mod"),
            Files: new[] { new ModFile("plugins/my_plugin.asi", 1024, "hash1") },
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        var assetMod = new StagedMod(
            Id: "asset_mod",
            Name: "Asset Mod",
            Version: "1.0",
            Description: "A test asset mod",
            LibraryPath: Path.Combine(_tempDir, "Library", "asset_mod"),
            Files: new[] { new ModFile("pc/data/cdimages/vehicles.img", 2048, "hash2") },
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        // Setup directories and files
        Directory.CreateDirectory(pluginMod.LibraryPath);
        Directory.CreateDirectory(Path.Combine(pluginMod.LibraryPath, "plugins"));
        await File.WriteAllTextAsync(Path.Combine(pluginMod.LibraryPath, "plugins", "my_plugin.asi"), "plugin code");

        Directory.CreateDirectory(assetMod.LibraryPath);
        Directory.CreateDirectory(Path.Combine(assetMod.LibraryPath, "pc", "data", "cdimages"));
        await File.WriteAllTextAsync(Path.Combine(assetMod.LibraryPath, "pc", "data", "cdimages", "vehicles.img"), "vehicles data");

        // Act: deploy
        await _adapter.DeployAsync(pluginMod, priority: 1);
        await _adapter.DeployAsync(assetMod, priority: 2);

        // Assert deployment exists
        string deployedPlugin = Path.Combine(_tempDir, "plugins", "01_my_plugin.asi");
        string deployedJunction = Path.Combine(_tempDir, "update", "002_AssetMod");
        Assert.True(File.Exists(deployedPlugin), "Deployed plugin file should exist.");
        Assert.True(Directory.Exists(deployedJunction), "Deployed junction directory should exist.");

        // Act: Undeploy
        await _adapter.UndeployAsync(pluginMod);
        await _adapter.UndeployAsync(assetMod);

        // Assert clean
        Assert.False(File.Exists(deployedPlugin), "Deployed plugin file should be deleted.");
        Assert.False(Directory.Exists(deployedJunction), "Deployed junction directory should be deleted.");
    }

    [Theory]
    [InlineData("1.0.4.0", false)]
    [InlineData("1.0.7.0", false)]
    [InlineData("1.0.8.0", false)]
    [InlineData("1, 0, 7, 0", false)]
    [InlineData(" 1.0.8.0 ", false)]
    [InlineData("1.0.7.0 (release)", false)]
    [InlineData("1.2.0.43", true)]
    [InlineData("1.2.0.32", true)]
    [InlineData("1.2.0.0", true)]
    [InlineData("1.0.5.0", true)]
    [InlineData("1.1.0.0", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void TestGameVersionDetection(string version, bool expectedIsCompleteEdition)
    {
        Assert.Equal(expectedIsCompleteEdition, GameVersionProfile.CheckIsCompleteEdition(version));
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
            // Ignore clean up errors
        }
    }
}
