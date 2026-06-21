using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ManagerIV.Core;

namespace ManagerIV.ViewModels;

public class MainViewModel : ViewModelBase
{
    // Services
    private readonly ArchiveHandler _archiveHandler;
    private readonly MetadataService _metadataService;
    private readonly ProfileManager _profileManager;
    private readonly LoadOrderService _loadOrderService;
    private readonly ConflictDetector _conflictDetector;
    private readonly NativeFileSystemLinker _linker;
    private readonly BackupRollbackService _rollbackService;
    private readonly UpdateWatchdog _watchdog;

    // Config Paths
    private readonly string _baseDir;
    private readonly string _libraryDir;
    private readonly string _profilesDir;
    private readonly string _backupDir;
    private readonly string _libraryManifestFile;

    // State Fields
    private ObservableCollection<Profile> _profiles = new();
    private Profile? _activeProfile;
    private ObservableCollection<ModViewModel> _libraryMods = new();
    private string _gameDir = "";
    private string _watchdogWarning = "";
    private bool _hasWatchdogWarning;
    private string _statusText = "Ready";
    private bool _isBusy;

    // Properties
    public ObservableCollection<Profile> Profiles
    {
        get => _profiles;
        set => SetProperty(ref _profiles, value);
    }

    public Profile? ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (SetProperty(ref _activeProfile, value) && value != null)
            {
                GameDir = value.GamePath;
                RefreshActiveModsList();
            }
        }
    }

    public ObservableCollection<ModViewModel> LibraryMods
    {
        get => _libraryMods;
        set => SetProperty(ref _libraryMods, value);
    }

    public string GameDir
    {
        get => _gameDir;
        set => SetProperty(ref _gameDir, value);
    }

    public string WatchdogWarning
    {
        get => _watchdogWarning;
        set
        {
            if (SetProperty(ref _watchdogWarning, value))
            {
                HasWatchdogWarning = !string.IsNullOrEmpty(value);
            }
        }
    }

    public bool HasWatchdogWarning
    {
        get => _hasWatchdogWarning;
        set => SetProperty(ref _hasWatchdogWarning, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    // Commands
    public ICommand ApplyDeploymentCommand { get; }
    public ICommand SwitchProfileCommand { get; }
    public ICommand CreateProfileCommand { get; }
    public ICommand ToggleModEnabledCommand { get; }
    public ICommand ImportModArchiveCommand { get; }
    public ICommand ReorderModCommand { get; }
    public ICommand SelectGameDirCommand { get; }

    public MainViewModel()
    {
        // Core initialization
        _archiveHandler = new ArchiveHandler();
        _metadataService = new MetadataService();
        _profileManager = new ProfileManager();
        _loadOrderService = new LoadOrderService();
        _conflictDetector = new ConflictDetector();
        _linker = new NativeFileSystemLinker();
        _watchdog = new UpdateWatchdog();

        // Establish paths
        _baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ManagerIV");
        _libraryDir = Path.Combine(_baseDir, "Library");
        _profilesDir = Path.Combine(_baseDir, "Profiles");
        _backupDir = Path.Combine(_baseDir, "Backup");
        _libraryManifestFile = Path.Combine(_libraryDir, "mods.json");

        Directory.CreateDirectory(_libraryDir);
        Directory.CreateDirectory(_profilesDir);
        Directory.CreateDirectory(_backupDir);

        _rollbackService = new BackupRollbackService(_linker, _backupDir);

        // Command bindings
        ApplyDeploymentCommand = new RelayCommand(async () => await ApplyDeploymentAsync(), () => !IsBusy && ActiveProfile != null);
        SwitchProfileCommand = new RelayCommand<Profile>(async (p) => await SwitchProfileAsync(p), (p) => !IsBusy && p != null);
        CreateProfileCommand = new RelayCommand<string>(CreateProfile, (name) => !string.IsNullOrWhiteSpace(name));
        ToggleModEnabledCommand = new RelayCommand<ModViewModel>(ToggleModEnabled);
        ImportModArchiveCommand = new RelayCommand(async () => await PromptAndImportArchiveAsync());
        ReorderModCommand = new RelayCommand<Tuple<ModViewModel, int>>(ReorderMod);
        SelectGameDirCommand = new RelayCommand(SelectGameDir, () => !IsBusy && ActiveProfile != null);

        // Load data
        LoadLibrary();
        LoadProfiles();
    }

    private void LoadLibrary()
    {
        LibraryMods.Clear();
        if (File.Exists(_libraryManifestFile))
        {
            try
            {
                string json = File.ReadAllText(_libraryManifestFile);
                var modsList = JsonSerializer.Deserialize<System.Collections.Generic.List<StagedMod>>(json);
                if (modsList != null)
                {
                    foreach (var mod in modsList)
                    {
                        // Default to update target, enabled = false, priority = 99
                        LibraryMods.Add(new ModViewModel(mod, false, 99, DeployTarget.Update));
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to load mod library manifest: {ex.Message}";
            }
        }
    }

    private void SaveLibrary()
    {
        try
        {
            var rawModels = LibraryMods.Select(m => m.Model).ToList();
            string json = JsonSerializer.Serialize(rawModels, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_libraryManifestFile, json);
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to save library manifest: {ex.Message}";
        }
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        var files = Directory.GetFiles(_profilesDir, "*.json");
        foreach (var file in files)
        {
            try
            {
                var profile = _profileManager.LoadProfile(file);
                Profiles.Add(profile);
            }
            catch
            {
                // Skip invalid profiles
            }
        }

        if (Profiles.Count == 0)
        {
            // Create default profile
            string defaultGamePath = @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto IV\GTAIV";
            var defaultProfile = new Profile(
                Id: Guid.NewGuid().ToString("N"),
                Name: "Default Complete Edition",
                GamePath: defaultGamePath,
                LibraryPath: _libraryDir,
                EnabledModIds: Array.Empty<string>(),
                LoadOrder: new LoadOrderModel(Array.Empty<LoadOrderEntry>()),
                ConflictState: new ConflictState(new System.Collections.Generic.Dictionary<string, ConflictInfo>(), Array.Empty<string>())
            );
            _profileManager.SaveProfile(Path.Combine(_profilesDir, "default.json"), defaultProfile);
            Profiles.Add(defaultProfile);
        }

        ActiveProfile = Profiles.First();
    }

    private void RefreshActiveModsList()
    {
        if (ActiveProfile == null) return;

        // Apply profile status to LibraryMods
        foreach (var modVm in LibraryMods)
        {
            modVm.IsEnabled = ActiveProfile.EnabledModIds.Contains(modVm.Id);
            
            var orderEntry = ActiveProfile.LoadOrder.Entries.FirstOrDefault(e => e.ModId == modVm.Id);
            if (orderEntry != null)
            {
                modVm.Priority = orderEntry.Priority;
                modVm.Target = orderEntry.Target;
            }
            else
            {
                // Default priorities
                modVm.Priority = 99;
                modVm.Target = modVm.Model.Files.Any(f => f.RelativePath.EndsWith(".asi", StringComparison.OrdinalIgnoreCase)) 
                    ? DeployTarget.Plugins 
                    : DeployTarget.Update;
            }
        }

        UpdateConflictsAndWatchdog();
    }

    private void UpdateConflictsAndWatchdog()
    {
        if (ActiveProfile == null) return;

        // Build sorted enabled mods list
        var enabledVms = LibraryMods.Where(m => m.IsEnabled).ToList();
        var enabledModels = enabledVms.Select(v => v.Model).ToList();

        // Make sure load order has all enabled entries
        var entries = enabledVms.Select(v => new LoadOrderEntry(v.Id, v.Target, v.Priority)).OrderBy(e => e.Priority).ToList();
        var currentLoadOrder = new LoadOrderModel(entries);

        // Detect conflicts
        var conflictState = _conflictDetector.DetectConflicts(enabledModels, currentLoadOrder);

        // Map conflicts back to ViewModels
        foreach (var modVm in LibraryMods)
        {
            var targetConflicts = conflictState.Conflicts.Values.Where(c => c.WinnerModId == modVm.Id || c.ConflictingModIds.Contains(modVm.Id)).ToList();
            if (targetConflicts.Any())
            {
                var losses = targetConflicts.Where(c => c.WinnerModId != modVm.Id).ToList();
                if (losses.Any())
                {
                    modVm.ConflictStatus = $"Overridden by {losses.Count} mod(s)";
                }
                else
                {
                    modVm.ConflictStatus = $"Overrides {targetConflicts.Sum(c => c.ConflictingModIds.Count)} mod(s)";
                }
            }
            else
            {
                modVm.ConflictStatus = "";
            }
        }

        // Trigger Watchdog check in background
        _ = RunWatchdogCheckAsync();
    }

    private async Task RunWatchdogCheckAsync()
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath) || !Directory.Exists(ActiveProfile.GamePath))
        {
            WatchdogWarning = "";
            return;
        }

        try
        {
            var result = await _watchdog.VerifyGameVersionAsync(ActiveProfile.GamePath, ActiveProfile);
            if (result.Status == WatchdogStatus.Mismatch)
            {
                WatchdogWarning = result.Message;
            }
            else if (result.Status == WatchdogStatus.NoLastKnownState && result.CurrentProfile != null)
            {
                // Capture first known version state and update profile
                var updatedProfile = ActiveProfile with { LastKnownVersion = result.CurrentProfile };
                SaveProfileState(updatedProfile);
                WatchdogWarning = "";
            }
            else
            {
                WatchdogWarning = "";
            }
        }
        catch
        {
            WatchdogWarning = "";
        }
    }

    private async Task PromptAndImportArchiveAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Mod Archives (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z|All Files (*.*)|*.*",
            Title = "Select Mod Archive to Import"
        };

        if (dialog.ShowDialog() == true)
        {
            await ImportArchiveAsync(dialog.FileName);
        }
    }

    private void SelectGameDir()
    {
        if (ActiveProfile == null) return;

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select GTA IV Game Directory (containing GTAIV.exe)",
            InitialDirectory = Directory.Exists(ActiveProfile.GamePath) ? ActiveProfile.GamePath : null
        };

        if (dialog.ShowDialog() == true)
        {
            string selectedPath = dialog.FolderName;
            
            // Check if GTAIV.exe exists
            string exePath = Path.Combine(selectedPath, "GTAIV.exe");
            if (!File.Exists(exePath))
            {
                var result = MessageBox.Show(
                    "GTAIV.exe was not found in the selected directory.\n\nAre you sure you want to select this directory anyway?",
                    "GTAIV.exe Not Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            var updatedProfile = ActiveProfile with { GamePath = selectedPath };
            SaveProfileState(updatedProfile);
            GameDir = selectedPath;
            RefreshActiveModsList();
            
            StatusText = $"Updated game directory to: {selectedPath}";
        }
    }

    public async Task ImportArchiveAsync(string archivePath)
    {
        if (ActiveProfile == null)
        {
            MessageBox.Show("Please select an active profile before importing mods.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        StatusText = $"Extracting archive '{Path.GetFileName(archivePath)}'...";

        try
        {
            // Parse temporary destination in library
            string tempGuid = Guid.NewGuid().ToString("N");
            string extractionTarget = Path.Combine(_libraryDir, tempGuid);

            // Extract with zip-slip protection
            await _archiveHandler.ExtractAsync(archivePath, extractionTarget);

            StatusText = "Analyzing compatibility and metadata...";
            
            // Scan folder
            var metadata = _metadataService.ScanExtractedDirectory(extractionTarget, Path.GetFileName(archivePath));

            // Move extraction folder to a clean mod name folder
            string cleanModName = string.Concat(metadata.Name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "");
            string finalModPath = Path.Combine(_libraryDir, cleanModName);
            if (Directory.Exists(finalModPath))
            {
                // Append suffix if mod name folder already exists
                finalModPath += "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            }

            Directory.Move(extractionTarget, finalModPath);

            // Build StagedMod record
            var stagedMod = new StagedMod(
                Id: Guid.NewGuid().ToString("N"),
                Name: metadata.Name,
                Version: metadata.Version,
                Description: metadata.Description,
                LibraryPath: finalModPath,
                Files: metadata.FileManifest.Select(f => new ModFile(f, new FileInfo(Path.Combine(finalModPath, f)).Length, null)).ToList(),
                IsEnabled: false,
                Compatibility: metadata.Compatibility
            );

            // Add to library
            var vm = new ModViewModel(stagedMod, false, 99, stagedMod.Files.Any(f => f.RelativePath.EndsWith(".asi", StringComparison.OrdinalIgnoreCase)) ? DeployTarget.Plugins : DeployTarget.Update);
            
            App.Current.Dispatcher.Invoke(() =>
            {
                LibraryMods.Add(vm);
                SaveLibrary();
                RefreshActiveModsList();
            });

            StatusText = $"Successfully imported mod '{metadata.Name}' (v{metadata.Version})!";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to import archive: {ex.Message}";
            MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ToggleModEnabled(ModViewModel? modVm)
    {
        if (modVm == null || ActiveProfile == null) return;

        modVm.IsEnabled = !modVm.IsEnabled;
        
        var list = ActiveProfile.EnabledModIds.ToList();
        if (modVm.IsEnabled)
        {
            if (!list.Contains(modVm.Id)) list.Add(modVm.Id);
        }
        else
        {
            list.Remove(modVm.Id);
        }

        // Build new load order representation
        var enabledMods = LibraryMods.Where(m => m.IsEnabled).ToList();
        var newOrder = _loadOrderService.InitializeLoadOrder(enabledMods.Select(m => m.Model));

        // Sync views back
        var updatedProfile = ActiveProfile with { EnabledModIds = list, LoadOrder = newOrder };
        SaveProfileState(updatedProfile);
        RefreshActiveModsList();
    }

    private void ReorderMod(Tuple<ModViewModel, int>? param)
    {
        if (param == null || ActiveProfile == null) return;

        var modVm = param.Item1;
        int targetPriority = param.Item2;

        var newOrder = _loadOrderService.ReorderMod(ActiveProfile.LoadOrder, modVm.Id, targetPriority);
        
        var updatedProfile = ActiveProfile with { LoadOrder = newOrder };
        SaveProfileState(updatedProfile);
        RefreshActiveModsList();
    }

    private void CreateProfile(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        string id = Guid.NewGuid().ToString("N");
        string defaultGamePath = ActiveProfile?.GamePath ?? @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto IV\GTAIV";
        
        var newProfile = new Profile(
            Id: id,
            Name: name,
            GamePath: defaultGamePath,
            LibraryPath: _libraryDir,
            EnabledModIds: Array.Empty<string>(),
            LoadOrder: new LoadOrderModel(Array.Empty<LoadOrderEntry>()),
            ConflictState: new ConflictState(new System.Collections.Generic.Dictionary<string, ConflictInfo>(), Array.Empty<string>())
        );

        string file = Path.Combine(_profilesDir, $"{id}.json");
        _profileManager.SaveProfile(file, newProfile);
        
        Profiles.Add(newProfile);
        ActiveProfile = newProfile;
        
        StatusText = $"Created and switched to profile '{name}'.";
    }

    private async Task SwitchProfileAsync(Profile newProfile)
    {
        if (ActiveProfile != null)
        {
            StatusText = "Tearing down current profile junctions...";
            // Undeploy old profile first
            var journal = new TransactionJournal();
            var adapter = new CompleteEditionAdapter(ActiveProfile.GamePath, _linker, journal);
            
            try
            {
                var enabledVms = LibraryMods.Where(m => ActiveProfile.EnabledModIds.Contains(m.Id)).ToList();
                foreach (var vm in enabledVms)
                {
                    await adapter.UndeployAsync(vm.Model);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Teardown warning: {ex.Message}";
            }
        }

        ActiveProfile = newProfile;
        StatusText = $"Switched active profile to '{newProfile.Name}'.";

        // Deploy new profile
        await ApplyDeploymentAsync();
    }

    private async Task ApplyDeploymentAsync()
    {
        if (ActiveProfile == null) return;

        IsBusy = true;
        StatusText = "Creating deployment Restore Point...";

        // Ensure directories exist in game dir
        try
        {
            Directory.CreateDirectory(Path.Combine(ActiveProfile.GamePath, "update"));
            Directory.CreateDirectory(Path.Combine(ActiveProfile.GamePath, "plugins"));
            Directory.CreateDirectory(Path.Combine(ActiveProfile.GamePath, "scripts"));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize game subfolders: {ex.Message}", "Deployment Error", MessageBoxButton.OK, MessageBoxImage.Error);
            IsBusy = false;
            return;
        }

        var journal = new TransactionJournal();
        var adapter = new CompleteEditionAdapter(ActiveProfile.GamePath, _linker, journal);

        try
        {
            // 1. Undeploy currently active links
            StatusText = "Clearing physical links...";
            foreach (var modVm in LibraryMods)
            {
                await adapter.UndeployAsync(modVm.Model);
            }

            // 2. Deploy enabled links in load order priority sequence
            StatusText = "Applying junctions and hard links...";
            var sortedEnabled = LibraryMods
                .Where(m => m.IsEnabled)
                .OrderBy(m => m.Priority)
                .ToList();

            foreach (var vm in sortedEnabled)
            {
                StatusText = $"Deploying '{vm.Name}' (Priority {vm.Priority})...";
                await adapter.DeployAsync(vm.Model, vm.Priority);
            }

            // 3. Update game watchdog reference
            StatusText = "Updating watchdog version reference...";
            var currentVersion = await _watchdog.CaptureCurrentVersionAsync(ActiveProfile.GamePath);
            var finalProfile = ActiveProfile with { LastKnownVersion = currentVersion };
            SaveProfileState(finalProfile);

            StatusText = "Deployment applied successfully!";
            MessageBox.Show("Mod load order deployed successfully! Game files are completely untouched.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText = "Deployment failed! Rolling back changes...";
            _rollbackService.Rollback(journal);
            StatusText = "Rollback completed. Prior system state restored.";
            MessageBox.Show($"Deployment failed: {ex.Message}\nAll filesystem changes have been automatically rolled back safely.", "Deployment Failure", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            UpdateConflictsAndWatchdog();
        }
    }

    private void SaveProfileState(Profile profile)
    {
        string file = Path.Combine(_profilesDir, $"{profile.Id}.json");
        _profileManager.SaveProfile(file, profile);
        
        // Sync collection
        int idx = Profiles.IndexOf(Profiles.First(p => p.Id == profile.Id));
        if (idx >= 0)
        {
            Profiles[idx] = profile;
        }
        _activeProfile = profile;
        OnPropertyChanged(nameof(ActiveProfile));
    }
}
