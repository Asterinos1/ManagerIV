using System.IO;
using System.IO.Compression;
using System.Net.Http;
using ManagerIV.Core;
using ManagerIV.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace ManagerIV.Tests;

public class LibertyTrainerTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly string _gameDir;
    private readonly string _downloadsDir;
    private readonly string _assetsDir;
    private readonly LibertyTrainerValidator _validator;
    private readonly LibertyTrainerDownloadMonitor _downloadMonitor;
    private readonly BackendToolManager _backendToolManager;
    private readonly LibertyTrainerDependencyService _dependencyService;
    private readonly LibertyTrainerInstaller _installer;

    public LibertyTrainerTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "ManagerIV_LibertyTests_" + Guid.NewGuid().ToString("N"));
        _gameDir = Path.Combine(_testBaseDir, "Game");
        _downloadsDir = Path.Combine(_testBaseDir, "Downloads");
        _assetsDir = Path.Combine(_testBaseDir, "Assets", "ScriptHook");

        Directory.CreateDirectory(_testBaseDir);
        Directory.CreateDirectory(_gameDir);
        Directory.CreateDirectory(_downloadsDir);
        Directory.CreateDirectory(_assetsDir);

        // Setup mock assets for ScriptHook and CE Hook
        File.WriteAllText(Path.Combine(_assetsDir, "ScriptHook.dll"), "Mock ScriptHook binary content");
        File.WriteAllText(Path.Combine(_assetsDir, "aCompleteEditionHook.asi"), "Mock CE Hook binary content");

        _validator = new LibertyTrainerValidator();
        _downloadMonitor = new LibertyTrainerDownloadMonitor(_validator);
        _backendToolManager = new BackendToolManager(Path.Combine(_testBaseDir, "Cache"), new HttpClient(new MockHttpMessageHandler(req => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)))));
        _dependencyService = new LibertyTrainerDependencyService(_backendToolManager, _assetsDir);
        _installer = new LibertyTrainerInstaller(_validator);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, recursive: true);
            }
        }
        catch { }
    }

    #region Helper ZIP Creation Methods

    private static void CreateValidRootZip(string zipPath)
    {
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var asiEntry = zip.CreateEntry("Liberty's Legacy.asi");
        using (var writer = new StreamWriter(asiEntry.Open()))
        {
            writer.Write("Mock Trainer ASI Binary Content");
        }

        var iniEntry = zip.CreateEntry("Liberty's Legacy/Liberty's Legacy.ini");
        using (var writer = new StreamWriter(iniEntry.Open()))
        {
            writer.Write("[Settings]\nMenuKey=122\nGodMode=0\n");
        }

        var dataEntry = zip.CreateEntry("Liberty's Legacy/Data/Vehicles.dat");
        using (var writer = new StreamWriter(dataEntry.Open()))
        {
            writer.Write("Turismo, Infernus, Comet");
        }
    }

    private static void CreateValidWrapperZip(string zipPath, string wrapperDir = "Liberty's Legacy Trainer 2.4.1")
    {
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var asiEntry = zip.CreateEntry($"{wrapperDir}/Liberty's Legacy.asi");
        using (var writer = new StreamWriter(asiEntry.Open()))
        {
            writer.Write("Mock Trainer ASI Binary Content");
        }

        var iniEntry = zip.CreateEntry($"{wrapperDir}/Liberty's Legacy/Liberty's Legacy.ini");
        using (var writer = new StreamWriter(iniEntry.Open()))
        {
            writer.Write("[Settings]\nMenuKey=122\nGodMode=0\n");
        }
    }

    private static void CreateMissingAsiZip(string zipPath)
    {
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var iniEntry = zip.CreateEntry("Liberty's Legacy/Liberty's Legacy.ini");
        using (var writer = new StreamWriter(iniEntry.Open()))
        {
            writer.Write("[Settings]\nMenuKey=122\n");
        }
    }

    private static void CreateMissingCompanionZip(string zipPath)
    {
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var asiEntry = zip.CreateEntry("Liberty's Legacy.asi");
        using (var writer = new StreamWriter(asiEntry.Open()))
        {
            writer.Write("Mock Trainer ASI Binary Content");
        }
    }

    private static void CreateMultipleAsiZip(string zipPath)
    {
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var asi1 = zip.CreateEntry("Liberty's Legacy.asi");
        using (var writer = new StreamWriter(asi1.Open())) { writer.Write("ASI 1"); }

        var asi2 = zip.CreateEntry("AnotherTrainer.asi");
        using (var writer = new StreamWriter(asi2.Open())) { writer.Write("ASI 2"); }

        var ini = zip.CreateEntry("Liberty's Legacy/config.ini");
        using (var writer = new StreamWriter(ini.Open())) { writer.Write("ini"); }
    }

    private static void CreatePathTraversalZip(string zipPath)
    {
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var entry = zip.CreateEntry("../malicious.asi");
        using (var writer = new StreamWriter(entry.Open())) { writer.Write("hack"); }
    }

    private static void CreateRootedPathZip(string zipPath)
    {
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var entry = zip.CreateEntry("/rooted/Liberty's Legacy.asi");
        using (var writer = new StreamWriter(entry.Open())) { writer.Write("rooted"); }
    }

    private static void CreateDriveLetterZip(string zipPath)
    {
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var entry = zip.CreateEntry("C:/Windows/System32/Liberty's Legacy.asi");
        using (var writer = new StreamWriter(entry.Open())) { writer.Write("drive letter"); }
    }

    private static void CreateDuplicateEntriesZip(string zipPath)
    {
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var asi1 = zip.CreateEntry("Liberty's Legacy.asi");
        using (var writer = new StreamWriter(asi1.Open())) { writer.Write("ASI 1"); }

        var asi2 = zip.CreateEntry("liberty's legacy.asi");
        using (var writer = new StreamWriter(asi2.Open())) { writer.Write("ASI 2 duplicate"); }

        var ini = zip.CreateEntry("Liberty's Legacy/config.ini");
        using (var writer = new StreamWriter(ini.Open())) { writer.Write("ini"); }
    }

    private static void CreateCaseCollisionZip(string zipPath)
    {
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var asi = zip.CreateEntry("Liberty's Legacy.asi");
        using (var writer = new StreamWriter(asi.Open())) { writer.Write("ASI"); }

        var ini1 = zip.CreateEntry("Liberty's Legacy/Data.txt");
        using (var writer = new StreamWriter(ini1.Open())) { writer.Write("Data 1"); }

        var ini2 = zip.CreateEntry("Liberty's Legacy/DATA.TXT");
        using (var writer = new StreamWriter(ini2.Open())) { writer.Write("Data 2 duplicate"); }
    }

    private static void CreateExcessiveEntriesZip(string zipPath, int count)
    {
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var asi = zip.CreateEntry("Liberty's Legacy.asi");
        using (var writer = new StreamWriter(asi.Open())) { writer.Write("ASI"); }

        var ini = zip.CreateEntry("Liberty's Legacy/config.ini");
        using (var writer = new StreamWriter(ini.Open())) { writer.Write("ini"); }

        for (int i = 0; i < count; i++)
        {
            var e = zip.CreateEntry($"Liberty's Legacy/dummy_{i}.txt");
            using var writer = new StreamWriter(e.Open());
            writer.Write("x");
        }
    }

    #endregion

    #region Tests 1-10: Validator Tests

    [Fact]
    public void Test1_ValidArchiveWithFilesAtRoot()
    {
        string zip = Path.Combine(_testBaseDir, "valid_root.zip");
        CreateValidRootZip(zip);

        var result = _validator.ValidateArchive(zip);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal("", result.ResolvedRootPrefix);
        Assert.Equal("Liberty's Legacy.asi", result.TrainerAsiEntryKey);
        Assert.NotEmpty(result.CompanionEntryKeys!);
    }

    [Fact]
    public void Test2_ValidArchiveWithOneWrapperDirectory()
    {
        string zip = Path.Combine(_testBaseDir, "valid_wrapper.zip");
        CreateValidWrapperZip(zip, "Liberty's Legacy Trainer 2.4.1");

        var result = _validator.ValidateArchive(zip);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal("Liberty's Legacy Trainer 2.4.1/", result.ResolvedRootPrefix);
        Assert.Equal("2.4.1", result.DetectedVersion);
    }

    [Fact]
    public void Test3_MissingTrainerAsi_FailsValidation()
    {
        string zip = Path.Combine(_testBaseDir, "missing_asi.zip");
        CreateMissingAsiZip(zip);

        var result = _validator.ValidateArchive(zip);

        Assert.False(result.IsValid);
        Assert.Contains("Missing required 'Liberty's Legacy.asi'", result.ErrorMessage);
    }

    [Fact]
    public void Test4_MissingCompanionDirectory_FailsValidation()
    {
        string zip = Path.Combine(_testBaseDir, "missing_companion.zip");
        CreateMissingCompanionZip(zip);

        var result = _validator.ValidateArchive(zip);

        Assert.False(result.IsValid);
        Assert.Contains("Missing required 'Liberty's Legacy' companion directory", result.ErrorMessage);
    }

    [Fact]
    public void Test5_MultipleCandidateAsis_FailsValidation()
    {
        string zip = Path.Combine(_testBaseDir, "multiple_asi.zip");
        CreateMultipleAsiZip(zip);

        var result = _validator.ValidateArchive(zip);

        Assert.False(result.IsValid);
        Assert.Contains("unexpected multiple ASI plugins", result.ErrorMessage);
    }

    [Fact]
    public void Test6_PathTraversal_FailsValidation()
    {
        string zip = Path.Combine(_testBaseDir, "path_traversal.zip");
        CreatePathTraversalZip(zip);

        var result = _validator.ValidateArchive(zip);

        Assert.False(result.IsValid);
        Assert.Contains("Potential path traversal", result.ErrorMessage);
    }

    [Fact]
    public void Test7_RootedAndDriveLetterPaths_FailsValidation()
    {
        string zip1 = Path.Combine(_testBaseDir, "rooted.zip");
        CreateRootedPathZip(zip1);
        var result1 = _validator.ValidateArchive(zip1);
        Assert.False(result1.IsValid);

        string zip2 = Path.Combine(_testBaseDir, "drive_letter.zip");
        CreateDriveLetterZip(zip2);
        var result2 = _validator.ValidateArchive(zip2);
        Assert.False(result2.IsValid);
    }

    [Fact]
    public void Test8_DuplicateNormalizedPaths_FailsValidation()
    {
        string zip = Path.Combine(_testBaseDir, "duplicate_paths.zip");
        CreateDuplicateEntriesZip(zip);

        var result = _validator.ValidateArchive(zip);

        Assert.False(result.IsValid);
        Assert.Contains("Duplicate normalized path or case collision", result.ErrorMessage);
    }

    [Fact]
    public void Test9_CaseInsensitiveCollisions_FailsValidation()
    {
        string zip = Path.Combine(_testBaseDir, "case_collision.zip");
        CreateCaseCollisionZip(zip);

        var result = _validator.ValidateArchive(zip);

        Assert.False(result.IsValid);
        Assert.Contains("Duplicate normalized path or case collision", result.ErrorMessage);
    }

    [Fact]
    public void Test10_ExcessiveEntryCount_FailsValidation()
    {
        string zip = Path.Combine(_testBaseDir, "excessive_entries.zip");
        CreateExcessiveEntriesZip(zip, LibertyTrainerValidator.MaxEntries + 10);

        var result = _validator.ValidateArchive(zip);

        Assert.False(result.IsValid);
        Assert.Contains("maximum allowed entry count limit", result.ErrorMessage);
    }

    #endregion

    #region Tests 11-14: Download Monitor Tests

    [Fact]
    public async Task Test11_UnrelatedZipIgnoredByWatcher()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        DateTime start = DateTime.UtcNow;

        // Create an unrelated zip
        string unrelatedZip = Path.Combine(_downloadsDir, "some_unrelated_mod.zip");
        using (var fs = File.Create(unrelatedZip))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var e = zip.CreateEntry("random_file.txt");
            using var writer = new StreamWriter(e.Open());
            writer.Write("unrelated data");
        }

        string? result = await _downloadMonitor.WaitForCandidateArchiveAsync(_downloadsDir, start, cts.Token);

        Assert.Null(result);
    }

    [Fact]
    public async Task Test12_IncompleteExtensionIgnored()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        DateTime start = DateTime.UtcNow;

        string partFile = Path.Combine(_downloadsDir, "LibertysLegacy.zip.crdownload");
        File.WriteAllText(partFile, "downloading...");

        string? result = await _downloadMonitor.WaitForCandidateArchiveAsync(_downloadsDir, start, cts.Token);

        Assert.Null(result);
    }

    [Fact]
    public async Task Test13_StableCompletedZipAccepted()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        DateTime start = DateTime.UtcNow;

        // Task to create valid zip shortly after monitoring starts
        _ = Task.Run(async () =>
        {
            await Task.Delay(400);
            string targetZip = Path.Combine(_downloadsDir, "LibertysLegacyTrainer_CE.zip");
            CreateValidRootZip(targetZip);
        });

        string? result = await _downloadMonitor.WaitForCandidateArchiveAsync(_downloadsDir, start, cts.Token);

        Assert.NotNull(result);
        Assert.True(File.Exists(result));
        Assert.Contains("LibertysLegacyTrainer_CE.zip", result);
    }

    [Fact]
    public async Task Test14_CancellationDisposesWatcher()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        DateTime start = DateTime.UtcNow;

        string? result = await _downloadMonitor.WaitForCandidateArchiveAsync(_downloadsDir, start, cts.Token);

        Assert.Null(result);
    }

    #endregion

    #region Tests 15-19: Dependency Resolution Tests

    [Fact]
    public async Task Test15_FusionFixSatisfiesUalDependency()
    {
        // Setup FusionFix in plugins folder
        string pluginsDir = Path.Combine(_gameDir, "plugins");
        Directory.CreateDirectory(pluginsDir);
        File.WriteAllText(Path.Combine(pluginsDir, "GTAIV.EFLC.FusionFix.asi"), "Mock FusionFix ASI");

        var profile = new Profile("p1", "Default", _gameDir, _testBaseDir, Array.Empty<string>(), new LoadOrderModel(Array.Empty<LoadOrderEntry>()), new ConflictState(new Dictionary<string, ConflictInfo>(), Array.Empty<string>()));
        var manifest = new List<InstalledToolFile>();

        var installed = await _dependencyService.EnsureDependenciesAsync(_gameDir, profile, manifest);

        // UAL was satisfied by FusionFix, so dinput8.dll should not have been installed as standalone
        Assert.DoesNotContain(installed, f => f.InstalledPath.EndsWith("dinput8.dll", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Test16_StandaloneUalSatisfiesDependency()
    {
        // Setup standalone dinput8.dll in game root
        File.WriteAllText(Path.Combine(_gameDir, "dinput8.dll"), "Mock Standalone UAL");

        var profile = new Profile("p1", "Default", _gameDir, _testBaseDir, Array.Empty<string>(), new LoadOrderModel(Array.Empty<LoadOrderEntry>()), new ConflictState(new Dictionary<string, ConflictInfo>(), Array.Empty<string>()));
        var manifest = new List<InstalledToolFile>();

        var installed = await _dependencyService.EnsureDependenciesAsync(_gameDir, profile, manifest);

        // Should not install another UAL
        Assert.DoesNotContain(installed, f => f.SourceTool == "ASILoader");
    }

    [Fact]
    public async Task Test17_MissingUalInvokesInstaller()
    {
        var profile = new Profile("p1", "Default", _gameDir, _testBaseDir, Array.Empty<string>(), new LoadOrderModel(Array.Empty<LoadOrderEntry>()), new ConflictState(new Dictionary<string, ConflictInfo>(), Array.Empty<string>()));
        var manifest = new List<InstalledToolFile>();

        bool ualCallbackCalled = false;
        Func<Task> ualInstaller = () =>
        {
            ualCallbackCalled = true;
            File.WriteAllText(Path.Combine(_gameDir, "dinput8.dll"), "Installed UAL");
            manifest.Add(new InstalledToolFile("ASILoader", Path.Combine(_gameDir, "dinput8.dll"), "hash"));
            return Task.CompletedTask;
        };

        await _dependencyService.EnsureDependenciesAsync(_gameDir, profile, manifest, ualInstaller);

        Assert.True(ualCallbackCalled);
        Assert.True(File.Exists(Path.Combine(_gameDir, "dinput8.dll")));
    }

    [Fact]
    public async Task Test18_ExistingScriptHookAndCeHookPreserved()
    {
        string existingShContent = "Original User Custom ScriptHook";
        string existingCeContent = "Original User Custom CE Hook";

        File.WriteAllText(Path.Combine(_gameDir, "ScriptHook.dll"), existingShContent);
        File.WriteAllText(Path.Combine(_gameDir, "aCompleteEditionHook.asi"), existingCeContent);
        File.WriteAllText(Path.Combine(_gameDir, "dinput8.dll"), "UAL");

        var profile = new Profile("p1", "Default", _gameDir, _testBaseDir, Array.Empty<string>(), new LoadOrderModel(Array.Empty<LoadOrderEntry>()), new ConflictState(new Dictionary<string, ConflictInfo>(), Array.Empty<string>()));
        var manifest = new List<InstalledToolFile>();

        var newlyInstalled = await _dependencyService.EnsureDependenciesAsync(_gameDir, profile, manifest);

        // No new files installed
        Assert.Empty(newlyInstalled);
        // Existing files untouched
        Assert.Equal(existingShContent, File.ReadAllText(Path.Combine(_gameDir, "ScriptHook.dll")));
        Assert.Equal(existingCeContent, File.ReadAllText(Path.Combine(_gameDir, "aCompleteEditionHook.asi")));
    }

    [Fact]
    public async Task Test19_OnlyMissingDependenciesInstalled()
    {
        // ScriptHook.dll and UAL already exist, but aCompleteEditionHook.asi is missing
        File.WriteAllText(Path.Combine(_gameDir, "ScriptHook.dll"), "Existing SH");
        File.WriteAllText(Path.Combine(_gameDir, "dinput8.dll"), "Existing UAL");

        var profile = new Profile("p1", "Default", _gameDir, _testBaseDir, Array.Empty<string>(), new LoadOrderModel(Array.Empty<LoadOrderEntry>()), new ConflictState(new Dictionary<string, ConflictInfo>(), Array.Empty<string>()));
        var manifest = new List<InstalledToolFile>();

        var newlyInstalled = await _dependencyService.EnsureDependenciesAsync(_gameDir, profile, manifest);

        Assert.Single(newlyInstalled);
        Assert.Equal(Path.Combine(_gameDir, "aCompleteEditionHook.asi"), newlyInstalled[0].InstalledPath);
        Assert.True(File.Exists(Path.Combine(_gameDir, "aCompleteEditionHook.asi")));
    }

    #endregion

    #region Tests 20-25: Trainer Deployment, Updates, Rollback, Uninstall, ViewModel Tests

    [Fact]
    public async Task Test20_TrainerAsiAndCompanionDirectoryDeployToGameRoot()
    {
        string zip = Path.Combine(_testBaseDir, "deploy_test.zip");
        CreateValidWrapperZip(zip, "Liberty's Legacy Trainer 2.4.1");

        var profile = new Profile("p1", "Default", _gameDir, _testBaseDir, Array.Empty<string>(), new LoadOrderModel(Array.Empty<LoadOrderEntry>()), new ConflictState(new Dictionary<string, ConflictInfo>(), Array.Empty<string>()));
        var manifest = new List<InstalledToolFile>();

        string ver = await _installer.InstallTrainerAsync(zip, _gameDir, profile, manifest);

        Assert.Equal("2.4.1", ver);
        Assert.True(File.Exists(Path.Combine(_gameDir, "Liberty's Legacy.asi")));
        Assert.True(File.Exists(Path.Combine(_gameDir, "Liberty's Legacy", "Liberty's Legacy.ini")));
        Assert.Contains(manifest, f => f.InstalledPath == Path.Combine(_gameDir, "Liberty's Legacy.asi"));
        Assert.Contains(manifest, f => f.InstalledPath == Path.Combine(_gameDir, "Liberty's Legacy", "Liberty's Legacy.ini"));
    }

    [Fact]
    public async Task Test21_UpdatePreservesModifiedIni()
    {
        string zip1 = Path.Combine(_testBaseDir, "v1.zip");
        CreateValidRootZip(zip1);

        var profile = new Profile("p1", "Default", _gameDir, _testBaseDir, Array.Empty<string>(), new LoadOrderModel(Array.Empty<LoadOrderEntry>()), new ConflictState(new Dictionary<string, ConflictInfo>(), Array.Empty<string>()));
        var manifest = new List<InstalledToolFile>();

        await _installer.InstallTrainerAsync(zip1, _gameDir, profile, manifest);

        // User customizes INI file
        string iniPath = Path.Combine(_gameDir, "Liberty's Legacy", "Liberty's Legacy.ini");
        string customizedIni = "[Settings]\nMenuKey=123\nGodMode=1\nCustomUserBinding=True\n";
        File.WriteAllText(iniPath, customizedIni);

        // Update with new version
        string zip2 = Path.Combine(_testBaseDir, "v2.zip");
        CreateValidWrapperZip(zip2, "Liberty's Legacy Trainer 2.4.2");

        await _installer.InstallTrainerAsync(zip2, _gameDir, profile, manifest);

        // User's customized INI should still be intact
        Assert.Equal(customizedIni, File.ReadAllText(iniPath));
    }

    [Fact]
    public async Task Test22_FailedUpdateRestoresPreviousTrainer()
    {
        string zip1 = Path.Combine(_testBaseDir, "initial.zip");
        CreateValidRootZip(zip1);

        var profile = new Profile("p1", "Default", _gameDir, _testBaseDir, Array.Empty<string>(), new LoadOrderModel(Array.Empty<LoadOrderEntry>()), new ConflictState(new Dictionary<string, ConflictInfo>(), Array.Empty<string>()));
        var manifest = new List<InstalledToolFile>();

        await _installer.InstallTrainerAsync(zip1, _gameDir, profile, manifest);

        string originalAsiContent = File.ReadAllText(Path.Combine(_gameDir, "Liberty's Legacy.asi"));

        // Attempt installation of corrupted/bad archive
        string badZip = Path.Combine(_testBaseDir, "bad.zip");
        CreateMissingAsiZip(badZip);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await _installer.InstallTrainerAsync(badZip, _gameDir, profile, manifest);
        });

        // Original installation state should be restored
        Assert.True(File.Exists(Path.Combine(_gameDir, "Liberty's Legacy.asi")));
        Assert.Equal(originalAsiContent, File.ReadAllText(Path.Combine(_gameDir, "Liberty's Legacy.asi")));
    }

    [Fact]
    public async Task Test23_UninstallPreservesUserDataByDefault()
    {
        string zip = Path.Combine(_testBaseDir, "install_for_uninstall.zip");
        CreateValidRootZip(zip);

        var profile = new Profile("p1", "Default", _gameDir, _testBaseDir, Array.Empty<string>(), new LoadOrderModel(Array.Empty<LoadOrderEntry>()), new ConflictState(new Dictionary<string, ConflictInfo>(), Array.Empty<string>()));
        var manifest = new List<InstalledToolFile>();

        await _installer.InstallTrainerAsync(zip, _gameDir, profile, manifest);

        // User adds a custom save/outfit file
        string customSave = Path.Combine(_gameDir, "Liberty's Legacy", "outfit_niko.json");
        File.WriteAllText(customSave, "{\"Outfit\": \"Leather Jacket\"}");

        // Perform uninstall with preserveUserData: true
        await _installer.UninstallTrainerAsync(_gameDir, manifest, preserveUserData: true);

        // ASI deleted
        Assert.False(File.Exists(Path.Combine(_gameDir, "Liberty's Legacy.asi")));
        // User created outfit preserved
        Assert.True(File.Exists(customSave));
        // Manifest cleaned
        Assert.DoesNotContain(manifest, f => f.SourceTool == "LibertysLegacy");
    }

    [Fact]
    public void Test24_PartialInstallationReportsRepairNeeded()
    {
        // Only ASI exists
        File.WriteAllText(Path.Combine(_gameDir, "Liberty's Legacy.asi"), "ASI only");
        var status1 = _installer.GetTrainerStatus(_gameDir);
        Assert.Equal(TrainerStatus.RepairNeeded, status1);

        // Only Companion folder exists
        File.Delete(Path.Combine(_gameDir, "Liberty's Legacy.asi"));
        string companionDir = Path.Combine(_gameDir, "Liberty's Legacy");
        Directory.CreateDirectory(companionDir);
        File.WriteAllText(Path.Combine(companionDir, "config.ini"), "config");

        var status2 = _installer.GetTrainerStatus(_gameDir);
        Assert.Equal(TrainerStatus.RepairNeeded, status2);

        // Both exist
        File.WriteAllText(Path.Combine(_gameDir, "Liberty's Legacy.asi"), "ASI");
        var status3 = _installer.GetTrainerStatus(_gameDir);
        Assert.Equal(TrainerStatus.Installed, status3);
    }

    [Fact]
    public async Task Test25_ViewModelCommandAndStatusBehavior()
    {
        var linker = new NativeFileSystemLinker();
        var archiveHandler = new ArchiveHandler();
        var metadataService = new MetadataService();
        var profileManager = new ProfileManager();
        var loadOrderService = new LoadOrderService();
        var conflictDetector = new ConflictDetector();
        var rollbackService = new BackupRollbackService(linker, Path.Combine(_testBaseDir, "Backup"));
        var watchdog = new UpdateWatchdog();
        var modStructureAnalyzer = new ModStructureAnalyzer();
        var saveProfileLogger = NullLogger<SaveProfileViewModel>.Instance;
        var saveProfileVM = new SaveProfileViewModel(saveProfileLogger);
        var libraryVM = new LibraryViewModel();
        var mainLogger = NullLogger<MainViewModel>.Instance;

        var vm = new MainViewModel(
            _testBaseDir,
            archiveHandler,
            metadataService,
            profileManager,
            loadOrderService,
            conflictDetector,
            linker,
            rollbackService,
            watchdog,
            _backendToolManager,
            modStructureAnalyzer,
            saveProfileVM,
            libraryVM,
            mainLogger,
            _validator,
            _downloadMonitor,
            _dependencyService,
            _installer
        );

        // Create and select profile
        var profile = new Profile("profile_test", "CE Profile", _gameDir, _testBaseDir, Array.Empty<string>(), new LoadOrderModel(Array.Empty<LoadOrderEntry>()), new ConflictState(new Dictionary<string, ConflictInfo>(), Array.Empty<string>()));
        vm.ActiveProfile = profile;

        // Verify initial status
        Assert.Equal(TrainerStatus.Missing, vm.BackendStatus.LibertyTrainerStatus);
        Assert.Equal("Get Trainer", vm.BackendStatus.LibertyTrainerButtonText);
        Assert.Equal("#FF8A0A0A", vm.BackendStatus.LibertyTrainerBrush);

        // Setup mock dinput8 to simulate pre-existing UAL
        File.WriteAllText(Path.Combine(_gameDir, "dinput8.dll"), "Mock dinput8 binary");

        // Create synthetic valid archive and install directly via explicit path
        string zip = Path.Combine(_testBaseDir, "test25_trainer.zip");
        CreateValidWrapperZip(zip, "Liberty's Legacy Trainer 2.4.1");

        await vm.InstallLibertyTrainerAsync(zip);

        // Verify updated status
        Assert.Equal(TrainerStatus.Installed, vm.BackendStatus.LibertyTrainerStatus);
        Assert.Equal("Update", vm.BackendStatus.LibertyTrainerButtonText);
        Assert.Equal("#FF107C41", vm.BackendStatus.LibertyTrainerBrush);
        Assert.True(File.Exists(Path.Combine(_gameDir, "Liberty's Legacy.asi")));
        Assert.NotNull(vm.OpenLibertyTrainerUrlCommand);
    }

    [Fact]
    public void Test26_FlatArchiveWithVersionInFilename_ExtractsVersion()
    {
        string zip = Path.Combine(_testBaseDir, "Libertys_Legacy_Trainer_v2.4.1.zip");
        CreateValidRootZip(zip);

        var result = _validator.ValidateArchive(zip);
        Assert.True(result.IsValid);
        Assert.Equal("2.4.1", result.DetectedVersion);
    }

    #endregion
}
