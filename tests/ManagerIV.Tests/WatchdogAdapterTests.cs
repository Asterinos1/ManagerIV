using System.IO;
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
        string deployedAssetFile = Path.Combine(_tempDir, "update", "pc", "data", "cdimages", "vehicles.img");
        Assert.True(File.Exists(deployedPlugin), "Deployed plugin file should exist.");
        Assert.False(Directory.Exists(deployedJunction), "Standard update-root asset files should not create a numbered update folder.");
        Assert.True(File.Exists(deployedAssetFile), "Standard update-root asset files should be merged directly.");

        // Act: Undeploy
        await _adapter.UndeployAsync(pluginMod);
        await _adapter.UndeployAsync(assetMod);

        // Assert clean
        Assert.False(File.Exists(deployedPlugin), "Deployed plugin file should be deleted.");
        Assert.False(Directory.Exists(deployedJunction), "Deployed junction directory should be deleted.");
        Assert.False(File.Exists(deployedAssetFile), "Directly merged asset file should be deleted.");
    }

    [Fact]
    public async Task TestCompleteEditionAdapterDeployAndUndeployNestedScripts()
    {
        // Arrange
        var scriptMod = new StagedMod(
            Id: "script_mod",
            Name: "Script Mod",
            Version: "1.0",
            Description: "A test script mod with nested subfolders",
            LibraryPath: Path.Combine(_tempDir, "Library", "script_mod"),
            Files: new[] { 
                new ModFile("scripts/NestedSubfolder/myscript.dll", 1024, "hash1"),
                new ModFile("scripts/NestedSubfolder/Assets/config.ini", 512, "hash2")
            },
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        Directory.CreateDirectory(Path.Combine(scriptMod.LibraryPath, "scripts", "NestedSubfolder", "Assets"));
        await File.WriteAllTextAsync(Path.Combine(scriptMod.LibraryPath, "scripts", "NestedSubfolder", "myscript.dll"), "dll code");
        await File.WriteAllTextAsync(Path.Combine(scriptMod.LibraryPath, "scripts", "NestedSubfolder", "Assets", "config.ini"), "config data");

        // Act: Deploy
        await _adapter.DeployAsync(scriptMod, priority: 1);

        // Assert: Verify nested directory structure is preserved in target scripts folder
        string targetDll = Path.Combine(_tempDir, "scripts", "NestedSubfolder", "myscript.dll");
        string targetIni = Path.Combine(_tempDir, "scripts", "NestedSubfolder", "Assets", "config.ini");

        Assert.True(File.Exists(targetDll), "Nested script dll should exist in target.");
        Assert.True(File.Exists(targetIni), "Nested script config should exist in target.");

        // Act: Undeploy
        await _adapter.UndeployAsync(scriptMod);

        // Assert: Verify clean up of files and empty parent subdirectories
        Assert.False(File.Exists(targetDll), "Undeployed nested script dll should be deleted.");
        Assert.False(File.Exists(targetIni), "Undeployed nested script config should be deleted.");
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "scripts", "NestedSubfolder")), "Empty parent subfolders should be removed.");
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

    [Fact]
    public async Task TestPlainStandardUpdateRootsDeployAsMergedFiles()
    {
        // Arrange
        var looseMod = new StagedMod(
            Id: "loose_mod",
            Name: "Loose Mod",
            Version: "1.0",
            Description: "A test loose files mod",
            LibraryPath: Path.Combine(_tempDir, "Library", "loose_mod"),
            Files: new[] { new ModFile("common/data/handling.dat", 1024, "hash1") },
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        Directory.CreateDirectory(looseMod.LibraryPath);
        Directory.CreateDirectory(Path.Combine(looseMod.LibraryPath, "common", "data"));
        await File.WriteAllTextAsync(Path.Combine(looseMod.LibraryPath, "common", "data", "handling.dat"), "handling data");

        // Act: deploy
        await _adapter.DeployAsync(looseMod, priority: 5);

        // Assert: Verify standard update-root files are merged directly
        string deployedJunction = Path.Combine(_tempDir, "update", "005_LooseMod");
        string directFile = Path.Combine(_tempDir, "update", "common", "data", "handling.dat");
        Assert.False(Directory.Exists(deployedJunction), "Pure standard update-root mods should not create a numbered junction.");
        Assert.True(File.Exists(directFile), "Standard update-root files should be merged directly into update/.");

        // Act: Undeploy
        await _adapter.UndeployAsync(looseMod);

        // Assert: Verify merged file is removed
        Assert.False(File.Exists(directFile), "Undeploy should remove directly merged standard update-root files.");
    }

    [Fact]
    public async Task TestRiversStyleStandardImgFilesDeployAsMergeOnly()
    {
        // Arrange
        var riversMod = new StagedMod(
            Id: "rivers_mod",
            Name: "Rivers Of Blood",
            Version: "8.1",
            Description: "A replacement-style effects mod",
            LibraryPath: Path.Combine(_tempDir, "Library", "rivers_mod"),
            Files: new[]
            {
                new ModFile("common/data/effects/bloodFx.dat", 100, "hash1"),
                new ModFile("common/data/materials/materials.dat", 100, "hash2"),
                new ModFile("pc/anim/anim1.img", 100, "hash3"),
                new ModFile("TBoGT/pc/anim/anim2.img", 100, "hash4"),
                new ModFile("TLAD/pc/anim/anim3.img", 100, "hash5"),
                new ModFile("pc/audio/sfx/resident.rpf", 100, "hash6"),
                new ModFile("pc/data/game.rpf", 100, "hash7"),
                new ModFile("pc/textures/peddamage.wtd", 100, "hash8")
            },
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        foreach (var file in riversMod.Files)
        {
            string path = Path.Combine(riversMod.LibraryPath, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, file.RelativePath);
        }

        Assert.True(UpdateDeploymentClassifier.IsMergeOnlyUpdateMod(riversMod));

        // Act
        await _adapter.DeployAsync(riversMod, priority: 11);

        // Assert
        string deployedJunction = Path.Combine(_tempDir, "update", "011_RiversOfBlood");
        string mergedAnimFile = Path.Combine(_tempDir, "update", "pc", "anim", "anim1.img");
        string mergedTbogtAnimFile = Path.Combine(_tempDir, "update", "TBoGT", "pc", "anim", "anim2.img");
        string mergedTladAnimFile = Path.Combine(_tempDir, "update", "TLAD", "pc", "anim", "anim3.img");
        string mergedEffectsFile = Path.Combine(_tempDir, "update", "common", "data", "effects", "bloodFx.dat");

        Assert.False(Directory.Exists(deployedJunction), "Merge-only standard update-root mods should not create a numbered update folder.");
        Assert.True(File.Exists(mergedAnimFile), "Real .img files under standard pc/ roots should merge directly into update/.");
        Assert.True(File.Exists(mergedTbogtAnimFile), "Real .img files under TBoGT standard roots should merge directly into update/.");
        Assert.True(File.Exists(mergedTladAnimFile), "Real .img files under TLAD standard roots should merge directly into update/.");
        Assert.True(File.Exists(mergedEffectsFile), "Standard common/ data files should merge directly into update/.");

        // Act
        await _adapter.UndeployAsync(riversMod);

        // Assert
        Assert.False(File.Exists(mergedAnimFile), "Undeploy should remove merged real .img files.");
        Assert.False(File.Exists(mergedTbogtAnimFile), "Undeploy should remove merged TBoGT real .img files.");
        Assert.False(File.Exists(mergedTladAnimFile), "Undeploy should remove merged TLAD real .img files.");
        Assert.False(File.Exists(mergedEffectsFile), "Undeploy should remove merged common/ data files.");
    }

    [Fact]
    public async Task TestMixedUpdateFolderMergesStandardRootsAndJunctionsImgSet()
    {
        // Arrange
        var mixedMod = new StagedMod(
            Id: "mixed_update_mod",
            Name: "Mixed Update Mod",
            Version: "1.0",
            Description: "A mixed update folder mod",
            LibraryPath: Path.Combine(_tempDir, "Library", "mixed_update_mod"),
            Files: new[]
            {
                new ModFile("pc/data/default.dat", 100, "hash1"),
                new ModFile("common/data/handling.dat", 100, "hash2"),
                new ModFile("LibertyAlive/vehicles.img/infernus.wft", 100, "hash3"),
                new ModFile("LibertyAlive/IV/vehicles.img/infernus.wft", 100, "hash4"),
                new ModFile("LibertyAlive/TLAD/vehicles.img/daemon.wft", 100, "hash5"),
                new ModFile("LibertyAlive/TBoGT/vehicles.img/buffalo.wft", 100, "hash6")
            },
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        foreach (var file in mixedMod.Files)
        {
            string path = Path.Combine(mixedMod.LibraryPath, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, file.RelativePath);
        }

        // Act
        await _adapter.DeployAsync(mixedMod, priority: 7);

        // Assert
        string mergedPcFile = Path.Combine(_tempDir, "update", "pc", "data", "default.dat");
        string mergedCommonFile = Path.Combine(_tempDir, "update", "common", "data", "handling.dat");
        string deployedJunction = Path.Combine(_tempDir, "update", "007_MixedUpdateMod");
        string nestedStandardFile = Path.Combine(deployedJunction, "pc", "data", "default.dat");
        string junctionImgFile = Path.Combine(deployedJunction, "IV", "vehicles.img", "infernus.wft");

        Assert.True(File.Exists(mergedPcFile), "Standard pc/ files should be merged directly into update/pc.");
        Assert.True(File.Exists(mergedCommonFile), "Standard common/ files should be merged directly into update/common.");
        Assert.True(Directory.Exists(deployedJunction), "The detected IMG set folder should be mounted as the numbered update folder.");
        Assert.False(File.Exists(nestedStandardFile), "Merged standard roots should not also appear inside the numbered mod folder.");
        Assert.True(File.Exists(junctionImgFile), "The numbered folder should expose the detected IMG virtual archive set.");

        // Act
        await _adapter.UndeployAsync(mixedMod);

        // Assert
        Assert.False(File.Exists(mergedPcFile), "Undeploy should remove merged standard pc/ files for split update mods.");
        Assert.False(File.Exists(mergedCommonFile), "Undeploy should remove merged standard common/ files for split update mods.");
        Assert.False(Directory.Exists(deployedJunction), "Undeploy should remove the numbered IMG set junction.");
    }

    [Fact]
    public async Task TestMixedUpdateFolderElevatesSingleImgPackFolder()
    {
        // Arrange
        var mixedMod = new StagedMod(
            Id: "single_img_pack_mod",
            Name: "Single Img Pack",
            Version: "1.0",
            Description: "A mixed update mod with one IMG pack folder",
            LibraryPath: Path.Combine(_tempDir, "Library", "single_img_pack_mod"),
            Files: new[]
            {
                new ModFile("common/data/handling.dat", 100, "hash1"),
                new ModFile("VehiclePack/vehicles.img/infernus.wft", 100, "hash2")
            },
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        foreach (var file in mixedMod.Files)
        {
            string path = Path.Combine(mixedMod.LibraryPath, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, file.RelativePath);
        }

        // Act
        await _adapter.DeployAsync(mixedMod, priority: 8);

        // Assert
        string mergedCommonFile = Path.Combine(_tempDir, "update", "common", "data", "handling.dat");
        string deployedJunction = Path.Combine(_tempDir, "update", "008_SingleImgPack");
        string nestedPackFile = Path.Combine(deployedJunction, "VehiclePack", "vehicles.img", "infernus.wft");
        string elevatedImgFile = Path.Combine(deployedJunction, "vehicles.img", "infernus.wft");

        Assert.True(File.Exists(mergedCommonFile), "Standard common/ files should be merged directly into update/common.");
        Assert.True(Directory.Exists(deployedJunction), "The single IMG pack folder should be mounted as the numbered update folder.");
        Assert.False(File.Exists(nestedPackFile), "The pack folder itself should be elevated, not nested below the numbered folder.");
        Assert.True(File.Exists(elevatedImgFile), "The numbered folder should expose the IMG pack contents directly.");
    }

    [Fact]
    public async Task TestUpdateFolderWithOnlySingleImgPackElevatesPackFolder()
    {
        // Arrange
        var imgOnlyMod = new StagedMod(
            Id: "img_only_mod",
            Name: "Img Only Mod",
            Version: "1.0",
            Description: "An update mod with one IMG pack folder",
            LibraryPath: Path.Combine(_tempDir, "Library", "img_only_mod"),
            Files: new[]
            {
                new ModFile("VehiclePack/vehicles.img/infernus.wft", 100, "hash1")
            },
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        foreach (var file in imgOnlyMod.Files)
        {
            string path = Path.Combine(imgOnlyMod.LibraryPath, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, file.RelativePath);
        }

        // Act
        await _adapter.DeployAsync(imgOnlyMod, priority: 9);

        // Assert
        string deployedJunction = Path.Combine(_tempDir, "update", "009_ImgOnlyMod");
        string nestedPackFile = Path.Combine(deployedJunction, "VehiclePack", "vehicles.img", "infernus.wft");
        string elevatedImgFile = Path.Combine(deployedJunction, "vehicles.img", "infernus.wft");

        Assert.True(Directory.Exists(deployedJunction), "The single IMG pack folder should be mounted as the numbered update folder.");
        Assert.False(File.Exists(nestedPackFile), "The pack folder itself should be elevated, not nested below the numbered folder.");
        Assert.True(File.Exists(elevatedImgFile), "The numbered folder should expose the IMG pack contents directly.");
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
