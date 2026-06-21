using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using ManagerIV.Core;

namespace ManagerIV.Tests;

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

    [Fact]
    public void TestActiveImgArchiveLimitCounter()
    {
        // 1. Arrange: setup a MainViewModel with a temporary directory
        string baseDir = Path.Combine(_tempDir, "vm_test");
        Directory.CreateDirectory(baseDir);
        
        var vm = new ManagerIV.ViewModels.MainViewModel(baseDir);
        Assert.NotNull(vm.ActiveProfile);
        Assert.Equal(0, vm.ActiveImgArchiveCount);
        Assert.Equal("Safe", vm.ActiveImgArchiveStatus);
        Assert.Equal("Safe", vm.ActiveImgArchiveSeverity);
        Assert.False(vm.ActiveImgArchiveHasWarning);

        // 2. Arrange: Create a mod with 30 .img files
        var files30 = Enumerable.Range(1, 30)
            .Select(i => new ModFile($"pc/models/cdimages/mod_{i}.img", 1024, $"hash{i}"))
            .ToList();
        var mod1 = new StagedMod(
            Id: "mod_1",
            Name: "Large Car Pack 1",
            Version: "1.0",
            Description: "30 cars",
            LibraryPath: Path.Combine(baseDir, "Library", "mod_1"),
            Files: files30,
            IsEnabled: false,
            Compatibility: "CE-compatible"
        );

        // Create a second mod with 15 .img files (total 45)
        var files15 = Enumerable.Range(1, 15)
            .Select(i => new ModFile($"pc/models/cdimages/extra_{i}.img", 1024, $"hash_ex{i}"))
            .ToList();
        var mod2 = new StagedMod(
            Id: "mod_2",
            Name: "Car Pack 2",
            Version: "1.0",
            Description: "15 cars",
            LibraryPath: Path.Combine(baseDir, "Library", "mod_2"),
            Files: files15,
            IsEnabled: false,
            Compatibility: "CE-compatible"
        );

        // Create a third mod with 10 .img files (total 55)
        var files10 = Enumerable.Range(1, 10)
            .Select(i => new ModFile($"pc/models/cdimages/super_{i}.img", 1024, $"hash_su{i}"))
            .ToList();
        var mod3 = new StagedMod(
            Id: "mod_3",
            Name: "Car Pack 3",
            Version: "1.0",
            Description: "10 cars",
            LibraryPath: Path.Combine(baseDir, "Library", "mod_3"),
            Files: files10,
            IsEnabled: false,
            Compatibility: "CE-compatible"
        );

        // Add them to VM's LibraryMods (simulating loaded library)
        var modVm1 = new ManagerIV.ViewModels.ModViewModel(mod1, false, 99, DeployTarget.Update);
        var modVm2 = new ManagerIV.ViewModels.ModViewModel(mod2, false, 99, DeployTarget.Update);
        var modVm3 = new ManagerIV.ViewModels.ModViewModel(mod3, false, 99, DeployTarget.Update);
        
        vm.LibraryMods.Add(modVm1);
        vm.LibraryMods.Add(modVm2);
        vm.LibraryMods.Add(modVm3);

        // 3. Act & Assert: Enable mod1 (30 img files -> Safe)
        vm.ToggleModEnabledCommand.Execute(modVm1);
        Assert.Equal(30, vm.ActiveImgArchiveCount);
        Assert.Equal("Safe", vm.ActiveImgArchiveSeverity);
        Assert.False(vm.ActiveImgArchiveHasWarning);

        // 4. Act & Assert: Enable mod2 (30 + 15 = 45 img files -> Danger Zone / Warning)
        vm.ToggleModEnabledCommand.Execute(modVm2);
        Assert.Equal(45, vm.ActiveImgArchiveCount);
        Assert.Equal("Warning", vm.ActiveImgArchiveSeverity);
        Assert.True(vm.ActiveImgArchiveHasWarning);
        Assert.Contains("Danger Zone", vm.ActiveImgArchiveStatus);
        Assert.Contains("Stability Limits", vm.ActiveImgArchiveWarningTitle);

        // 5. Act & Assert: Enable mod3 (45 + 10 = 55 img files -> Crash Risk / Danger)
        vm.ToggleModEnabledCommand.Execute(modVm3);
        Assert.Equal(55, vm.ActiveImgArchiveCount);
        Assert.Equal("Danger", vm.ActiveImgArchiveSeverity);
        Assert.True(vm.ActiveImgArchiveHasWarning);
        Assert.Contains("Crash Risk", vm.ActiveImgArchiveStatus);
        Assert.Contains("Limit Exceeded", vm.ActiveImgArchiveWarningTitle);

        // 6. Act & Assert: Disable mod2 and mod3 (should drop back to 30 -> Safe)
        vm.ToggleModEnabledCommand.Execute(modVm2);
        vm.ToggleModEnabledCommand.Execute(modVm3);
        Assert.Equal(30, vm.ActiveImgArchiveCount);
        Assert.Equal("Safe", vm.ActiveImgArchiveSeverity);
        Assert.False(vm.ActiveImgArchiveHasWarning);
    }

    [Fact]
    public void TestVramPresetSelectionCommand()
    {
        // Arrange
        string baseDir = Path.Combine(_tempDir, "vram_preset_test");
        Directory.CreateDirectory(baseDir);
        
        var vm = new ManagerIV.ViewModels.MainViewModel(baseDir);
        Assert.NotNull(vm.ActiveProfile);
        
        // Assert default
        Assert.Equal(2048, vm.GpuVramMb);

        // Act & Assert 4 GB preset
        vm.SetVramPresetCommand.Execute("4096");
        Assert.Equal(4096, vm.GpuVramMb);

        // Act & Assert 8 GB preset
        vm.SetVramPresetCommand.Execute("8192");
        Assert.Equal(8192, vm.GpuVramMb);

        // Act & Assert invalid input has no effect
        vm.SetVramPresetCommand.Execute("invalid");
        Assert.Equal(8192, vm.GpuVramMb);
    }

    [Fact]
    public async System.Threading.Tasks.Task TestClearLibraryCommand()
    {
        // Arrange
        string baseDir = Path.Combine(_tempDir, "clear_lib_test");
        Directory.CreateDirectory(baseDir);

        var vm = new ManagerIV.ViewModels.MainViewModel(baseDir);
        Assert.Empty(vm.LibraryMods);

        // Add a mock mod to the library list
        var fileA = new ModFile("data/handling.dat", 1024, "hashA");
        var modA = new StagedMod(
            Id: "mod_a",
            Name: "Handling Fix A",
            Version: "1.0",
            Description: "Mod A description",
            LibraryPath: Path.Combine(baseDir, "Library", "mod_a"),
            Files: new[] { fileA },
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );
        Directory.CreateDirectory(modA.LibraryPath);
        File.WriteAllText(Path.Combine(modA.LibraryPath, "handling.dat"), "mock content");

        var modVm = new ManagerIV.ViewModels.ModViewModel(modA, true, 1, DeployTarget.Update);
        vm.LibraryMods.Add(modVm);
        Assert.Single(vm.LibraryMods);

        // Act: Execute the internal method directly to avoid the UI Dialog popup
        await vm.ClearLibraryInternalAsync(showSuccessMessage: false);

        // Assert
        Assert.Empty(vm.LibraryMods);
        Assert.False(Directory.Exists(modA.LibraryPath));
    }

    [Fact]
    public void TestFusionFixConfigReadWrite()
    {
        // 1. Arrange: Create a temporary GTAIV.EFLC.FusionFix.ini content
        string iniPath = Path.Combine(_tempDir, "GTAIV.EFLC.FusionFix.ini");
        string originalIniContent = @"[MAIN]
RecoilFix = 1                                 // make recoil behavior the same as controller when playing with keyboard and mouse
AimingZoomFix = 1                             // -1: TBoGT aiming zoom behaves like IV and TLaD | 0: disable the fix | 1: TBoGT aiming zoom behaves like xbox feature | 2: have this feature also enabled in IV and TLaD

[CAMERASENSITIVITY]
MouseLookSensitivityRange = 0.1, 2.0          // min and max range for mouse look sensitivity slider
GamepadLookSensitivityRange = 0.1, 2.0        // min and max range for gamepad look sensitivity slider

[SHADOWS]
ExtraDynamicShadows = 2                       // adds back some missing shadows | 1: to some vegetation | 2: to some fences, grates, roads, walls and some vegetation
HighResolutionShadows = 0                     // doubles cascaded shadow map resolution, very GPU intensive
CascadeBlendSize = 0.1                        // controls the size of the cascade blending region
ForceShadowFilter = 0                         // shadow filter blur profile

[FRAMELIMIT]
FpsLimit = -2                                 // used when FPS Limit menu toggle is set to Custom (negative values use refresh rate as a base)
CutsceneFpsLimit = 0                          // custom FPS Limit value in cutscene
LoadingFpsLimit = 30                          // used to avoid game freeze on loading (e.g. Off Route mission)
UnlockFramerateDuringLoadscreens = 1          // game loads faster when using frame limiter
MinigamesFpsLimit = 30
MinigamesList = pool_game, air_hockey

[MISC]
ConsoleCarReflectionsAndDirt = 1              // use stronger console car reflections and any cars can be dirty when they are spawned like console
DeathMusic = 0                                // plays cut death music when player is dead in IV
DrunkDrivingHandlingFixIntensity = 0.65       // 0.0: unpatched | 1.0: post-derivation
DrunkDrivingCamFixIntensity = 1.0             // 0.0: unpatched | 1.0: scale
DefaultCameraAngleInTLaD = 1
PedDeathAnimFixFromTBoGT = 1
DisableCameraCenteringInCover = 1
ExtraInfo = 1
OverrideTreeAlpha = 0.0
AlwaysDisplayHealthOnReticle = 1
SmoothShorelines = 1
SmoothLightVolumes = 1
NoBloomColorShift = 1

[FOG]
VolFogFarClip = 4500.0
ExtendedTimecycEditing = 0

[BudgetedIV]
VehicleBudget = 0
PedBudget = 0
ExtendedLimits = 0                            // increases various limits

[EPISODICCONTENT]
EpisodicVehicles = 0
EpisodicWeapons = 0
ExplosiveAnnihilator = 0
OtherEpisodicChecks = 0
TBoGTHelicopterHeightLimit = 0
TBoGTPoliceWeapons = 0
RemoveSCOSignatureCheck = 0

[SUNSHAFTS]
SunShaftsDensity = 0.9
SunShaftsDecay = 0.95

[POSTFX]
EnablePreAlphaDepth = 1
AmbientOcclusionBlurPasses = 1
AmbientOcclusionSamples = 9
AmbientOcclusionLogMaxOffset = 3.0
AmbientOcclusionMaxMipLevel = 5
AmbientOcclusionFarClip = 150.0
AmbientOcclusionRadius = 1.125
AmbientOcclusionBias = 0.03
AmbientOcclusionIntensity = 0.4
AmbientOcclusionBlurRadius = 2.0

[SHADOWFILTERSHARP]                           // CE-like shadows
ShadowSoftness = 1.5                          // controls shadow blur
ShadowBias = 5.0                              // controls shadow bias, adjust according to softness

[SHADOWFILTERSOFT]                            // 1.0.4.0-like shadows
ShadowSoftness = 3.0                          // controls shadow blur
ShadowBias = 8.0                              // controls shadow bias, adjust according to softness

[SHADOWFILTERCHSS]                            // CHSS / PCSS
ShadowSoftness = 1.5                          // controls minimum shadow blur
ShadowBias = 5.0                              // controls shadow bias, adjust according to minimum softness
MaxSoftness = 20.0                            // controls maximum shadow blur

[PROJECT2DFX]
CoronaRadiusMultiplier = 1.0
CoronaAlphaMultiplier = 1.0
SlightlyIncreaseRadiusWithDistance = 1
DisableDefaultLodLights = 1

[TURNINDICATORS]
ManualTurnIndicators = 0                      // enable manual turn indicators, tap LB or RB on gamepad to activate
LeftIndicatorKey = 0xDB
RightIndicatorKey = 0xDD

[EXPERIMENTAL]
ReflectionMSAAQuality = 0
";
        File.WriteAllText(iniPath, originalIniContent);

        // 2. Act: Load config
        var config = FusionFixConfig.Load(iniPath);

        // Assert loaded values
        Assert.Equal(1, config.RecoilFix);
        Assert.Equal(1, config.AimingZoomFix);
        Assert.Equal(0.1, config.MouseLookSensitivityRangeMin);
        Assert.Equal(2.0, config.MouseLookSensitivityRangeMax);
        Assert.Equal(2, config.ExtraDynamicShadows);
        Assert.Equal(0.1, config.CascadeBlendSize);
        Assert.Equal(0, config.HighResolutionShadows);
        Assert.Equal(0, config.ForceShadowFilter);
        Assert.Equal(-2, config.FpsLimit);
        Assert.Equal(0, config.CutsceneFpsLimit);
        Assert.Equal(30, config.LoadingFpsLimit);
        Assert.Equal(1, config.UnlockFramerateDuringLoadscreens);
        Assert.Equal(30, config.MinigamesFpsLimit);
        Assert.Equal("pool_game, air_hockey", config.MinigamesList);
        Assert.Equal(1, config.ConsoleCarReflectionsAndDirt);
        Assert.Equal(0, config.DeathMusic);
        Assert.Equal(0.65, config.DrunkDrivingHandlingFixIntensity);
        Assert.Equal(1.0, config.DrunkDrivingCamFixIntensity);
        Assert.Equal(1, config.DefaultCameraAngleInTLaD);
        Assert.Equal(1, config.PedDeathAnimFixFromTBoGT);
        Assert.Equal(1, config.DisableCameraCenteringInCover);
        Assert.Equal(1, config.ExtraInfo);
        Assert.Equal(0.0, config.OverrideTreeAlpha);
        Assert.Equal(1, config.AlwaysDisplayHealthOnReticle);
        Assert.Equal(1, config.SmoothShorelines);
        Assert.Equal(1, config.SmoothLightVolumes);
        Assert.Equal(1, config.NoBloomColorShift);
        Assert.Equal(4500.0, config.VolFogFarClip);
        Assert.Equal(0, config.ExtendedTimecycEditing);
        Assert.Equal(0, config.VehicleBudget);
        Assert.Equal(0, config.PedBudget);
        Assert.Equal(0, config.ExtendedLimits);
        Assert.Equal(0, config.EpisodicVehicles);
        Assert.Equal(0, config.EpisodicWeapons);
        Assert.Equal(0, config.ExplosiveAnnihilator);
        Assert.Equal(0, config.OtherEpisodicChecks);
        Assert.Equal(0, config.TBoGTHelicopterHeightLimit);
        Assert.Equal(0, config.TBoGTPoliceWeapons);
        Assert.Equal(0, config.RemoveSCOSignatureCheck);
        Assert.Equal(0.9, config.SunShaftsDensity);
        Assert.Equal(0.95, config.SunShaftsDecay);
        Assert.Equal(1, config.EnablePreAlphaDepth);
        Assert.Equal(1, config.AmbientOcclusionBlurPasses);
        Assert.Equal(9, config.AmbientOcclusionSamples);
        Assert.Equal(3.0, config.AmbientOcclusionLogMaxOffset);
        Assert.Equal(5, config.AmbientOcclusionMaxMipLevel);
        Assert.Equal(150.0, config.AmbientOcclusionFarClip);
        Assert.Equal(1.125, config.AmbientOcclusionRadius);
        Assert.Equal(0.03, config.AmbientOcclusionBias);
        Assert.Equal(0.4, config.AmbientOcclusionIntensity);
        Assert.Equal(2.0, config.AmbientOcclusionBlurRadius);
        Assert.Equal(1.5, config.SharpShadowSoftness);
        Assert.Equal(5.0, config.SharpShadowBias);
        Assert.Equal(3.0, config.SoftShadowSoftness);
        Assert.Equal(8.0, config.SoftShadowBias);
        Assert.Equal(1.5, config.ChssShadowSoftness);
        Assert.Equal(5.0, config.ChssShadowBias);
        Assert.Equal(20.0, config.ChssMaxSoftness);
        Assert.Equal(1.0, config.CoronaRadiusMultiplier);
        Assert.Equal(1.0, config.CoronaAlphaMultiplier);
        Assert.Equal(1, config.SlightlyIncreaseRadiusWithDistance);
        Assert.Equal(1, config.DisableDefaultLodLights);
        Assert.Equal(0, config.ManualTurnIndicators);
        Assert.Equal("0xDB", config.LeftIndicatorKey);
        Assert.Equal("0xDD", config.RightIndicatorKey);
        Assert.Equal(0, config.ReflectionMSAAQuality);

        // 3. Act: Modify values
        config.RecoilFixEnabled = false; // -> RecoilFix = 0
        config.AimingZoomFix = 2;
        config.MouseLookSensitivityRangeMin = 0.5;
        config.MouseLookSensitivityRangeMax = 3.0;
        config.ExtraDynamicShadows = 0;
        config.HighResolutionShadowsEnabled = true; // -> HighResolutionShadows = 1
        config.SharpShadowSoftness = 2.5;
        config.SoftShadowSoftness = 4.5;
        config.ChssShadowSoftness = 1.0;
        config.ChssMaxSoftness = 25.0;
        config.ManualTurnIndicatorsEnabled = true; // -> 1
        config.LeftIndicatorKey = "0x41";

        // Save back
        FusionFixConfig.Save(iniPath, config);

        // 4. Assert: Read again and verify changes are persisted
        var reloaded = FusionFixConfig.Load(iniPath);
        Assert.Equal(0, reloaded.RecoilFix);
        Assert.Equal(2, reloaded.AimingZoomFix);
        Assert.Equal(0.5, reloaded.MouseLookSensitivityRangeMin);
        Assert.Equal(3.0, reloaded.MouseLookSensitivityRangeMax);
        Assert.Equal(0, reloaded.ExtraDynamicShadows);
        Assert.Equal(1, reloaded.HighResolutionShadows);
        Assert.Equal(2.5, reloaded.SharpShadowSoftness);
        Assert.Equal(4.5, reloaded.SoftShadowSoftness);
        Assert.Equal(1.0, reloaded.ChssShadowSoftness);
        Assert.Equal(25.0, reloaded.ChssMaxSoftness);
        Assert.Equal(1, reloaded.ManualTurnIndicators);
        Assert.Equal("0x41", reloaded.LeftIndicatorKey);

        // 5. Assert: Verify original formatting and comments are preserved
        string savedIniContent = File.ReadAllText(iniPath);
        Assert.Contains("// make recoil behavior the same as controller", savedIniContent);
        Assert.Contains("// min and max range for mouse look sensitivity", savedIniContent);
        Assert.Contains("// CE-like shadows", savedIniContent);
        Assert.Contains("// 1.0.4.0-like shadows", savedIniContent);
        Assert.Contains("MouseLookSensitivityRange = 0.5, 3.0", savedIniContent);
        Assert.Contains("LeftIndicatorKey = 0x41", savedIniContent);
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
