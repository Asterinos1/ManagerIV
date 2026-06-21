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
