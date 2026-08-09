using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ManagerIV.Core;
using Microsoft.Extensions.Logging;

namespace ManagerIV.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ILogger<MainViewModel> _logger;
    // Services
    private readonly ArchiveHandler _archiveHandler;
    private readonly MetadataService _metadataService;
    private readonly ProfileManager _profileManager;
    internal readonly LoadOrderService _loadOrderService;
    private readonly ConflictDetector _conflictDetector;
    internal readonly IFileSystemLinker _linker;
    private readonly BackupRollbackService _rollbackService;
    private readonly UpdateWatchdog _watchdog;
    private readonly BackendToolManager _backendToolManager;
    private readonly IModStructureAnalyzer _modStructureAnalyzer;

    // Config Paths
    private readonly string _baseDir;
    private readonly string _profilesDir;
    private readonly string _backupDir;
    internal readonly string _libraryDir;
    internal readonly string _libraryManifestFile;

    // State Fields
    private ObservableCollection<Profile> _profiles = new();
    private Profile? _activeProfile;
    private string _gameDir = "";
    private string _watchdogWarning = "";
    private bool _hasWatchdogWarning;
    private string _statusText = "Ready";
    private bool _isBusy;
    private BackendStatusViewModel _backendStatus = new();
    private bool _isDarkTheme = true;
    private MusicViewModel _music;
    private ModViewModel? _selectedMod;

    // Save Profiles fields
    private SaveProfileManager _saveProfileManager;
    private string _gtaSaveProfilesPath = "";
    private ObservableCollection<string> _baseProfileIds = new();
    private string? _selectedBaseProfileId;
    private ObservableCollection<SaveProfile> _saveProfiles = new();
    private SaveProfile? _selectedSaveProfile;
    private string _newSaveProfileName = "";
    private string _renameActiveSaveTo = "";

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
                OnPropertyChanged(nameof(GpuVramMb));
                OnPropertyChanged(nameof(GameVersionDisplayString));
                LoadFusionFixConfig();
            }
        }
    }

    public string GameVersionDisplayString
    {
        get
        {
            if (ActiveProfile?.LastKnownVersion == null)
            {
                return "Detected: Unknown Version";
            }
            if (ActiveProfile.LastKnownVersion.IsCompleteEdition)
            {
                return "Detected: Complete Edition";
            }
            return $"Detected: Legacy {ActiveProfile.LastKnownVersion.Version}";
        }
    }

    public ObservableCollection<ActiveMergedUpdateFileViewModel> ActiveMergedUpdateFiles { get; } = new();

    public int ActiveMergedUpdateFileCount => ActiveMergedUpdateFiles.Count;

    public ModViewModel? SelectedMod
    {
        get => _selectedMod;
        set => SetProperty(ref _selectedMod, value);
    }

    public string GameDir
    {
        get => _gameDir;
        set => SetProperty(ref _gameDir, value);
    }

    public int GpuVramMb
    {
        get
        {
            if (ActiveProfile == null) return 2048;
            return ActiveProfile.GpuVramMb == 0 ? 2048 : ActiveProfile.GpuVramMb;
        }
        set
        {
            if (ActiveProfile != null && GpuVramMb != value)
            {
                var updatedProfile = ActiveProfile with { GpuVramMb = value };
                SaveProfileState(updatedProfile);
                OnPropertyChanged(nameof(GpuVramMb));
            }
        }
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
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public BackendStatusViewModel BackendStatus
    {
        get => _backendStatus;
        set => SetProperty(ref _backendStatus, value);
    }

    public MusicViewModel Music
    {
        get => _music;
        set => SetProperty(ref _music, value);
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
            {
                ApplyTheme(value);
                SaveSettings();
            }
        }
    }

    public string GtaSaveProfilesPath
    {
        get => _gtaSaveProfilesPath;
        set
        {
            if (SetProperty(ref _gtaSaveProfilesPath, value))
            {
                _saveProfileManager = new SaveProfileManager(value);
                LoadSaveProfilesData();
                SaveSettings();
            }
        }
    }

    public ObservableCollection<string> BaseProfileIds
    {
        get => _baseProfileIds;
        set => SetProperty(ref _baseProfileIds, value);
    }

    public bool HasBaseProfiles => BaseProfileIds.Count > 0;

    public string? SelectedBaseProfileId
    {
        get => _selectedBaseProfileId;
        set
        {
            if (SetProperty(ref _selectedBaseProfileId, value))
            {
                RefreshSaveProfilesList();
            }
        }
    }

    public ObservableCollection<SaveProfile> SaveProfiles
    {
        get => _saveProfiles;
        set => SetProperty(ref _saveProfiles, value);
    }

    public SaveProfile? SelectedSaveProfile
    {
        get => _selectedSaveProfile;
        set => SetProperty(ref _selectedSaveProfile, value);
    }

    public string NewSaveProfileName
    {
        get => _newSaveProfileName;
        set => SetProperty(ref _newSaveProfileName, value);
    }

    private string _quickSnapshotName = "";
    public string QuickSnapshotName
    {
        get => _quickSnapshotName;
        set => SetProperty(ref _quickSnapshotName, value);
    }

    public string RenameActiveSaveTo
    {
        get => _renameActiveSaveTo;
        set => SetProperty(ref _renameActiveSaveTo, value);
    }

    private int _selectedIndex;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }

    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                LibraryVM.ApplyFilter();
            }
        }
    }

    private int _activeImgArchiveCount;
    private string _activeImgArchiveStatus = "Safe";
    private string _activeImgArchiveSeverity = "Safe";
    private string _activeImgArchiveWarningTitle = "";
    private string _activeImgArchiveWarningDescription = "";
    private bool _activeImgArchiveHasWarning;

    public int ActiveImgArchiveCount
    {
        get => _activeImgArchiveCount;
        set => SetProperty(ref _activeImgArchiveCount, value);
    }

    public string ActiveImgArchiveStatus
    {
        get => _activeImgArchiveStatus;
        set => SetProperty(ref _activeImgArchiveStatus, value);
    }

    public string ActiveImgArchiveSeverity
    {
        get => _activeImgArchiveSeverity;
        set => SetProperty(ref _activeImgArchiveSeverity, value);
    }

    public string ActiveImgArchiveWarningTitle
    {
        get => _activeImgArchiveWarningTitle;
        set => SetProperty(ref _activeImgArchiveWarningTitle, value);
    }

    public string ActiveImgArchiveWarningDescription
    {
        get => _activeImgArchiveWarningDescription;
        set => SetProperty(ref _activeImgArchiveWarningDescription, value);
    }

    public bool ActiveImgArchiveHasWarning
    {
        get => _activeImgArchiveHasWarning;
        set => SetProperty(ref _activeImgArchiveHasWarning, value);
    }

    private FusionFixConfig _fusionFixSettings = new();
    private bool _isFusionFixConfigAvailable;

    public FusionFixConfig FusionFixSettings
    {
        get => _fusionFixSettings;
        set
        {
            if (value == null) return;
            if (_fusionFixSettings != value)
            {
                if (_fusionFixSettings != null)
                {
                    _fusionFixSettings.PropertyChanged -= FusionFixSettings_PropertyChanged;
                }
                _fusionFixSettings = value;
                OnPropertyChanged(nameof(FusionFixSettings));
                _fusionFixSettings.PropertyChanged += FusionFixSettings_PropertyChanged;
                OnPropertyChanged(nameof(VehicleBudgetWarning));
                OnPropertyChanged(nameof(HasVehicleBudgetWarning));
            }
        }
    }

    private void FusionFixSettings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FusionFixConfig.VehicleBudget))
        {
            OnPropertyChanged(nameof(VehicleBudgetWarning));
            OnPropertyChanged(nameof(HasVehicleBudgetWarning));
        }
    }

    public string VehicleBudgetWarning
    {
        get
        {
            if (FusionFixSettings == null) return string.Empty;
            int val = FusionFixSettings.VehicleBudget;
            if (val > 300000000)
            {
                return "⚠️ Warning: Budget exceeds 300MB limit. This may starve the audio pool (causing muted engine sounds) or trigger severe texture popping and crashes.";
            }
            if (val > 0 && val < 120000000)
            {
                return "ℹ️ Note: Standard taxi bug fix recommends at least 120,000,000 bytes (120MB) to 150,000,000 bytes (150MB).";
            }
            return string.Empty;
        }
    }

    public bool HasVehicleBudgetWarning => !string.IsNullOrEmpty(VehicleBudgetWarning);

    public bool IsFusionFixConfigAvailable
    {
        get => _isFusionFixConfigAvailable;
        set
        {
            if (SetProperty(ref _isFusionFixConfigAvailable, value))
            {
                OnPropertyChanged(nameof(IsBackendConfigAvailable));
            }
        }
    }

    private DxvkConfig _dxvkSettings = new();
    private bool _isDxvkConfigAvailable;
    private bool _isFusionFixTabActive = true;

    public DxvkConfig DxvkSettings
    {
        get => _dxvkSettings;
        set => SetProperty(ref _dxvkSettings, value);
    }

    public bool IsDxvkConfigAvailable
    {
        get => _isDxvkConfigAvailable;
        set
        {
            if (SetProperty(ref _isDxvkConfigAvailable, value))
            {
                OnPropertyChanged(nameof(IsBackendConfigAvailable));
            }
        }
    }

    public bool IsBackendConfigAvailable => IsFusionFixConfigAvailable || IsDxvkConfigAvailable;

    public bool IsFusionFixTabActive
    {
        get => _isFusionFixTabActive;
        set
        {
            if (SetProperty(ref _isFusionFixTabActive, value))
            {
                OnPropertyChanged(nameof(IsDxvkTabActive));
            }
        }
    }

    public bool IsDxvkTabActive => !IsFusionFixTabActive;

    // Commands
    public ICommand ApplyDeploymentCommand { get; }
    public ICommand SwitchProfileCommand { get; }
    public ICommand CreateProfileCommand { get; }
    public ICommand SelectGameDirCommand { get; }
    public ICommand RemoveProfileCommand { get; }
    public ICommand RenameProfileCommand { get; }
    public ICommand InstallFusionFixCommand { get; }
    public ICommand UninstallFusionFixCommand { get; }
    public ICommand InstallAsiLoaderCommand { get; }
    public ICommand UninstallAsiLoaderCommand { get; }
    public ICommand InstallDxvkCommand { get; }
    public ICommand UninstallDxvkCommand { get; }
    public ICommand InstallScriptHookCommand { get; }
    public ICommand UninstallScriptHookCommand { get; }
    public ICommand InstallMemBiterCommand { get; }
    public ICommand UninstallMemBiterCommand { get; }
    public ICommand InstallBassAudioCommand { get; }
    public ICommand UninstallBassAudioCommand { get; }
    public ICommand SetVramPresetCommand { get; }
    public ICommand ResetGameDirectoryCommand { get; }
    public ICommand SaveFusionFixConfigCommand { get; }
    public ICommand LoadFusionFixDefaultsCommand { get; }
    public ICommand RefreshFusionFixConfigCommand { get; }
    public ICommand SwitchBackendTabCommand { get; }
    public ICommand SetVehicleBudgetPresetCommand { get; }
    public ICommand AutoCalculateVehicleBudgetCommand { get; }
    public ICommand BrowseSaveProfilesPathCommand { get; }
    public ICommand ActivateSaveProfileCommand { get; }
    public ICommand CreateSaveProfileCommand { get; }
    public ICommand RenameSaveProfileCommand { get; }
    public ICommand DeleteSaveProfileCommand { get; }
    public ICommand RefreshSaveProfilesCommand { get; }
    public ICommand OpenGameDirCommand { get; }
    public ICommand QuickSnapshotSaveProfileCommand { get; }
    public ICommand OpenSaveProfileFolderCommand { get; }
    public ICommand ImportSaveFileCommand { get; }

    public SaveProfileViewModel SaveProfileVM { get; }
    public LibraryViewModel LibraryVM { get; }

    public MainViewModel(
        string baseDir,
        ArchiveHandler archiveHandler,
        MetadataService metadataService,
        ProfileManager profileManager,
        LoadOrderService loadOrderService,
        ConflictDetector conflictDetector,
        IFileSystemLinker linker,
        BackupRollbackService rollbackService,
        UpdateWatchdog watchdog,
        BackendToolManager backendToolManager,
        IModStructureAnalyzer modStructureAnalyzer,
        SaveProfileViewModel saveProfileVM,
        LibraryViewModel libraryVM,
        ILogger<MainViewModel> logger)
    {
        _logger = logger;
        LibraryVM = libraryVM;
        LibraryVM.MainVM = this;
        _baseDir = baseDir;
        _archiveHandler = archiveHandler;
        _metadataService = metadataService;
        _profileManager = profileManager;
        _loadOrderService = loadOrderService;
        _conflictDetector = conflictDetector;
        _linker = linker;
        _rollbackService = rollbackService;
        _watchdog = watchdog;
        _backendToolManager = backendToolManager;
        _modStructureAnalyzer = modStructureAnalyzer;
        SaveProfileVM = saveProfileVM;

        // Establish paths
        _libraryDir = Path.Combine(_baseDir, "Library");
        _profilesDir = Path.Combine(_baseDir, "Profiles");
        _backupDir = Path.Combine(_baseDir, "Backup");
        _libraryManifestFile = Path.Combine(_libraryDir, "mods.json");

        Directory.CreateDirectory(_libraryDir);
        Directory.CreateDirectory(_profilesDir);
        Directory.CreateDirectory(_backupDir);

        // Command bindings
        ApplyDeploymentCommand = new RelayCommand(async () => await ApplyDeploymentAsync(), () => !IsBusy && ActiveProfile != null);
        SwitchProfileCommand = new RelayCommand<Profile>(async (p) => await SwitchProfileAsync(p), (p) => !IsBusy && p != null);
        CreateProfileCommand = new RelayCommand<string>(CreateProfile, (name) => !string.IsNullOrWhiteSpace(name));
        SelectGameDirCommand = new RelayCommand(SelectGameDir, () => !IsBusy && ActiveProfile != null);
        RemoveProfileCommand = new RelayCommand(RemoveActiveProfile, () => !IsBusy && ActiveProfile != null && Profiles.Count > 1);
        RenameProfileCommand = new RelayCommand<string>(RenameActiveProfile, (name) => !IsBusy && ActiveProfile != null && !string.IsNullOrWhiteSpace(name));
        InstallFusionFixCommand = new RelayCommand(async () => await InstallFusionFixAsync(), () => !IsBusy && ActiveProfile != null);
        UninstallFusionFixCommand = new RelayCommand(async () => await UninstallFusionFixAsync(), () => !IsBusy && ActiveProfile != null);
        InstallAsiLoaderCommand = new RelayCommand(async () => await InstallAsiLoaderAsync(), () => !IsBusy && ActiveProfile != null);
        UninstallAsiLoaderCommand = new RelayCommand(async () => await UninstallAsiLoaderAsync(), () => !IsBusy && ActiveProfile != null);
        InstallDxvkCommand = new RelayCommand(async () => await InstallDxvkAsync(), () => !IsBusy && ActiveProfile != null);
        UninstallDxvkCommand = new RelayCommand(async () => await UninstallDxvkAsync(), () => !IsBusy && ActiveProfile != null);
        InstallScriptHookCommand = new RelayCommand(async () => await InstallScriptHookAsync(), () => !IsBusy && ActiveProfile != null);
        UninstallScriptHookCommand = new RelayCommand(async () => await UninstallScriptHookAsync(), () => !IsBusy && ActiveProfile != null);
        InstallMemBiterCommand = new RelayCommand(async () => await InstallMemBiterAsync(), () => !IsBusy && ActiveProfile != null);
        UninstallMemBiterCommand = new RelayCommand(async () => await UninstallMemBiterAsync(), () => !IsBusy && ActiveProfile != null);
        InstallBassAudioCommand = new RelayCommand(async () => await InstallBassAudioAsync(), () => !IsBusy && ActiveProfile != null);
        UninstallBassAudioCommand = new RelayCommand(async () => await UninstallBassAudioAsync(), () => !IsBusy && ActiveProfile != null);
        SetVramPresetCommand = new RelayCommand<object>(SetVramPreset);
        ResetGameDirectoryCommand = new RelayCommand(async () => await ResetGameDirectoryAsync(), () => !IsBusy && ActiveProfile != null && !string.IsNullOrEmpty(ActiveProfile.GamePath));
        SaveFusionFixConfigCommand = new RelayCommand(SaveFusionFixConfig, () => IsBackendConfigAvailable && !IsBusy);
        LoadFusionFixDefaultsCommand = new RelayCommand(LoadDefaults, () => !IsBusy);
        RefreshFusionFixConfigCommand = new RelayCommand(RefreshFusionFixConfig, () => IsBackendConfigAvailable && !IsBusy);
        SwitchBackendTabCommand = new RelayCommand<string>(tab =>
        {
            if (tab == "FusionFix") IsFusionFixTabActive = true;
            else if (tab == "DXVK") IsFusionFixTabActive = false;
        });
        SetVehicleBudgetPresetCommand = new RelayCommand<object>(SetVehicleBudgetPreset);
        AutoCalculateVehicleBudgetCommand = new RelayCommand(AutoCalculateVehicleBudget, () => IsFusionFixConfigAvailable && ActiveProfile != null && !IsBusy);

        BrowseSaveProfilesPathCommand = new RelayCommand(BrowseSaveProfilesPath);
        ActivateSaveProfileCommand = new RelayCommand<object>(ActivateSaveProfile);
        CreateSaveProfileCommand = new RelayCommand(CreateSaveProfile);
        RenameSaveProfileCommand = new RelayCommand<string>(RenameSaveProfile);
        DeleteSaveProfileCommand = new RelayCommand(DeleteSaveProfile);
        RefreshSaveProfilesCommand = new RelayCommand(RefreshSaveProfilesList);
        OpenGameDirCommand = new RelayCommand(OpenGameDirInExplorer, () => ActiveProfile != null && !string.IsNullOrWhiteSpace(ActiveProfile.GamePath) && Directory.Exists(ActiveProfile.GamePath));
        QuickSnapshotSaveProfileCommand = new RelayCommand(QuickSnapshotSaveProfile, () => !string.IsNullOrEmpty(SelectedBaseProfileId));
        OpenSaveProfileFolderCommand = new RelayCommand<SaveProfile>(OpenSaveProfileFolder, (sp) => (sp ?? SelectedSaveProfile) != null && Directory.Exists((sp ?? SelectedSaveProfile)!.FullPath));
        ImportSaveFileCommand = new RelayCommand<object>(ImportSaveFile, (param) => !string.IsNullOrEmpty(SelectedBaseProfileId));

        // Initialize collection views and their filters (only if running inside Application context to avoid WPF threading issues in unit tests)
        if (System.Windows.Application.Current != null)
        {

            LibraryVM.MainModsCollection.Filter = (obj) =>
            {
                if (obj is ModViewModel mod)
                {
                    bool matchesTarget = mod.Target == DeployTarget.Update;
                    bool matchesSearch = string.IsNullOrWhiteSpace(SearchQuery) || mod.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
                    return matchesTarget && matchesSearch;
                }
                return false;
            };

            LibraryVM.PluginsCollection.Filter = (obj) =>
            {
                if (obj is ModViewModel mod)
                {
                    bool matchesTarget = mod.Target == DeployTarget.Plugins;
                    bool matchesSearch = string.IsNullOrWhiteSpace(SearchQuery) || mod.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
                    return matchesTarget && matchesSearch;
                }
                return false;
            };

            LibraryVM.ScriptsCollection.Filter = (obj) =>
            {
                if (obj is ModViewModel mod)
                {
                    bool matchesTarget = mod.Target == DeployTarget.Scripts;
                    bool matchesSearch = string.IsNullOrWhiteSpace(SearchQuery) || mod.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
                    return matchesTarget && matchesSearch;
                }
                return false;
            };
        }

        // Load data
        LibraryVM.LoadLibrary();
        LoadProfiles();

        // Load settings
        string settingsFile = Path.Combine(_baseDir, "settings.json");
        if (File.Exists(settingsFile))
        {
            try
            {
                string json = File.ReadAllText(settingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    _isDarkTheme = true; // Always Dark Theme
                    _gtaSaveProfilesPath = settings.GtaSaveProfilesPath ?? "";
                }
            }
            catch { }
        }
        ApplyTheme(_isDarkTheme);

        _saveProfileManager = new SaveProfileManager(_gtaSaveProfilesPath);
        LoadSaveProfilesData();
        _music = new MusicViewModel(_baseDir, _linker);
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
                if (Path.GetFileName(file).Equals("default.json", StringComparison.OrdinalIgnoreCase))
                {
                    string newPath = Path.Combine(_profilesDir, $"{profile.Id}.json");
                    _profileManager.SaveProfile(newPath, profile);
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignore delete failure for migration
                    }
                }
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
            _profileManager.SaveProfile(Path.Combine(_profilesDir, $"{defaultProfile.Id}.json"), defaultProfile);
            Profiles.Add(defaultProfile);
        }

        ActiveProfile = Profiles.First();
    }

    internal void RefreshActiveModsList()
    {
        if (ActiveProfile == null) return;

        var entries = ActiveProfile.LoadOrder.Entries.ToList();
        bool loadOrderChanged = false;

        // Ensure every mod in LibraryVM.LibraryMods has a valid, unique LoadOrder entry
        foreach (var modVm in LibraryVM.LibraryMods)
        {
            modVm.IsEnabled = ActiveProfile.EnabledModIds.Contains(modVm.Id);
            
            // Determine the correct target based on files
            DeployTarget correctTarget = _loadOrderService.DetermineDeployTarget(modVm.Model);

            var orderEntry = entries.FirstOrDefault(e => e.ModId == modVm.Id);
            if (orderEntry != null)
            {
                modVm.Priority = orderEntry.Priority;
                
                // Migrate legacy incorrect targets (e.g. scripts default-saved as Update)
                if (orderEntry.Target != correctTarget)
                {
                    modVm.Target = correctTarget;
                    int idx = entries.IndexOf(orderEntry);
                    entries[idx] = orderEntry with { Target = correctTarget };
                    loadOrderChanged = true;
                }
                else
                {
                    modVm.Target = orderEntry.Target;
                }
            }
            else
            {
                modVm.Target = correctTarget;
                
                var sameTypeEntries = entries.Where(e => e.Target == correctTarget).ToList();
                int maxPriority = sameTypeEntries.Any() ? sameTypeEntries.Max(e => e.Priority) : 0;
                modVm.Priority = maxPriority + 1;
                
                entries.Add(new LoadOrderEntry(modVm.Id, modVm.Target, modVm.Priority));
                loadOrderChanged = true;
            }
        }

        // Clean up entries for mods that no longer exist in LibraryVM.LibraryMods
        var libraryModIds = LibraryVM.LibraryMods.Select(m => m.Id).ToHashSet();
        int initialCount = entries.Count;
        entries.RemoveAll(e => !libraryModIds.Contains(e.ModId));
        if (entries.Count != initialCount)
        {
            loadOrderChanged = true;
        }

        // Re-sequence all entries in the load order to ensure they are 1..N contiguous per target type
        var plugins = entries.Where(e => e.Target == DeployTarget.Plugins).OrderBy(e => e.Priority).Select((entry, index) => entry with { Priority = index + 1 }).ToList();
        var scripts = entries.Where(e => e.Target == DeployTarget.Scripts).OrderBy(e => e.Priority).Select((entry, index) => entry with { Priority = index + 1 }).ToList();
        var mods = entries.Where(e => e.Target == DeployTarget.Update).OrderBy(e => e.Priority).Select((entry, index) => entry with { Priority = index + 1 }).ToList();
        var resequencedEntries = plugins.Concat(scripts).Concat(mods).ToList();

        // Check if resequencing changed any priorities
        if (!loadOrderChanged && resequencedEntries.Count == entries.Count)
        {
            for (int i = 0; i < resequencedEntries.Count; i++)
            {
                if (resequencedEntries[i].Priority != entries[i].Priority)
                {
                    loadOrderChanged = true;
                    break;
                }
            }
        }

        if (loadOrderChanged)
        {
            var updatedProfile = ActiveProfile with { LoadOrder = new LoadOrderModel(resequencedEntries) };
            SaveProfileState(updatedProfile);
        }

        // Apply the resequenced priorities back to modViewModels
        foreach (var modVm in LibraryVM.LibraryMods)
        {
            var entry = resequencedEntries.FirstOrDefault(e => e.ModId == modVm.Id);
            if (entry != null)
            {
                modVm.Priority = entry.Priority;
                modVm.Target = entry.Target;
            }
        }

        // Sort LibraryVM.LibraryMods collection by Priority
        var sorted = LibraryVM.LibraryMods.OrderBy(m => m.Priority).ToList();
        LibraryVM.LibraryMods.Clear();
        foreach (var mod in sorted)
        {
            LibraryVM.LibraryMods.Add(mod);
        }

        UpdateConflictsAndWatchdog();
        LibraryVM.ApplyFilter();
    }

    internal void UpdateConflictsAndWatchdog()
    {
        if (ActiveProfile == null) return;

        UpdateBackendStatus();
        LoadFusionFixConfig();

        // Build sorted enabled mods list
        var enabledVms = LibraryVM.LibraryMods.Where(m => m.IsEnabled).ToList();
        var enabledModels = enabledVms.Select(v => v.Model).ToList();

        // Make sure load order has all enabled entries
        var entries = enabledVms.Select(v => new LoadOrderEntry(v.Id, v.Target, v.Priority)).OrderBy(e => e.Priority).ToList();
        var currentLoadOrder = new LoadOrderModel(entries);

        // Detect conflicts
        var conflictState = _conflictDetector.DetectConflicts(enabledModels, currentLoadOrder);

        ActiveMergedUpdateFiles.Clear();
        foreach (var modVm in enabledVms.Where(vm => vm.Target == DeployTarget.Update))
        {
            if (!UpdateDeploymentClassifier.ShouldMergeStandardUpdateRoots(modVm.Model))
            {
                continue;
            }

            foreach (var file in UpdateDeploymentClassifier.GetDirectUpdateMergeFiles(modVm.Model))
            {
                ActiveMergedUpdateFiles.Add(new ActiveMergedUpdateFileViewModel(
                    modVm.Name,
                    file.RelativePath.Replace('\\', '/')
                ));
            }
        }
        OnPropertyChanged(nameof(ActiveMergedUpdateFileCount));

        // Map conflicts and structure validations back to ViewModels
        var validator = new UpdateFolderValidator();
        foreach (var modVm in LibraryVM.LibraryMods)
        {
            var validationIssues = validator.Validate(modVm.Model);
            var targetConflicts = conflictState.Conflicts.Values.Where(c => c.WinnerModId == modVm.Id || c.ConflictingModIds.Contains(modVm.Id)).ToList();
            
            var details = new System.Collections.Generic.List<string>();
            
            // Add validation issues first
            foreach (var issue in validationIssues)
            {
                details.Add($"[{issue.Severity}] {issue.Message}");
            }
            
            // Add conflicts next
            string conflictSummary = "";
            if (targetConflicts.Any())
            {
                var losses = targetConflicts.Where(c => c.WinnerModId != modVm.Id).ToList();
                if (losses.Any())
                {
                    conflictSummary = $"Overridden by {losses.Count} mod(s)";
                    details.Add($"[Conflict] Overridden by other mods on {losses.Count} file(s):");
                    foreach (var loss in losses)
                    {
                        var winnerName = LibraryVM.LibraryMods.FirstOrDefault(m => m.Id == loss.WinnerModId)?.Name ?? "Another Mod";
                        details.Add($"  - '{loss.TargetPath}' overridden by '{winnerName}'");
                    }
                }
                else
                {
                    var winsCount = targetConflicts.Sum(c => c.ConflictingModIds.Count);
                    conflictSummary = $"Overrides {winsCount} mod(s)";
                    details.Add($"[Conflict] Overrides other mods on {targetConflicts.Count} file(s).");
                }
            }

            // Determine status label
            if (validationIssues.Any(i => i.Severity == "Error"))
            {
                modVm.ConflictStatus = "❌ Structure Error";
            }
            else if (validationIssues.Any(i => i.Severity == "Warning"))
            {
                modVm.ConflictStatus = "⚠️ Structure Warning";
            }
            else if (!string.IsNullOrEmpty(conflictSummary))
            {
                modVm.ConflictStatus = conflictSummary;
            }
            else
            {
                modVm.ConflictStatus = "";
            }
            
            modVm.ConflictDetails = details.Count > 0 ? string.Join(System.Environment.NewLine, details) : null;
        }

        // Calculate custom .img archive count
        int imgCount = 0;
        foreach (var modVm in LibraryVM.LibraryMods)
        {
            if (modVm.IsEnabled && modVm.Target == DeployTarget.Update)
            {
                var uniqueImgs = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var file in modVm.Model.Files)
                {
                    string relPath = file.RelativePath.Replace('\\', '/');
                    var parts = relPath.Split('/');
                    string currentPath = "";
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string part = parts[i];
                        if (string.IsNullOrEmpty(part)) continue;
                        
                        currentPath = currentPath == "" ? part : currentPath + "/" + part;
                        if (part.EndsWith(".img", System.StringComparison.OrdinalIgnoreCase))
                        {
                            uniqueImgs.Add(currentPath);
                            break; // .img component found (folder or leaf file); stop descending this branch
                        }
                    }
                }
                imgCount += uniqueImgs.Count;
            }
        }
        
        ActiveImgArchiveCount = imgCount;

        if (imgCount <= 40)
        {
            ActiveImgArchiveStatus = "Safe";
            ActiveImgArchiveSeverity = "Safe";
            ActiveImgArchiveHasWarning = false;
            ActiveImgArchiveWarningTitle = "";
            ActiveImgArchiveWarningDescription = "";
        }
        else if (imgCount <= 49)
        {
            ActiveImgArchiveStatus = "Danger Zone";
            ActiveImgArchiveSeverity = "Warning";
            ActiveImgArchiveHasWarning = true;
            ActiveImgArchiveWarningTitle = "⚠️ Danger Zone: Approaching Engine Stability Limits";
            ActiveImgArchiveWarningDescription = $"You have {imgCount} active custom .img files. Exceeding 50 custom archives inside the 'update' folder will overflow Grand Theft Auto IV's unmodifiable 8-bit index (max 255 archives total, including vanilla base), causing missing textures, disappearing traffic, and immediate crashes. Consider using OpenIV to merge/consolidate your modded vehicles or map archives.";
        }
        else
        {
            ActiveImgArchiveStatus = "Crash Risk";
            ActiveImgArchiveSeverity = "Danger";
            ActiveImgArchiveHasWarning = true;
            ActiveImgArchiveWarningTitle = "❌ Critical Limit Exceeded: Engine Crash Imminent!";
            ActiveImgArchiveWarningDescription = $"You have {imgCount} active custom .img files. Hitting 50+ custom archives overflows GTA IV's unmodifiable 8-bit index (maximum 255 archives total). Textures and cars will fail to load, followed by immediate stack overflow crashes. You MUST use OpenIV to consolidate your files (e.g., combine multiple individual vehicle .img archives into a single custom_vehicles.img archive) to stay below the limit.";
        }

        // Trigger Watchdog check in background
        _ = RunWatchdogCheckAsync();
    }

    private void UpdateBackendStatus()
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath) || !Directory.Exists(ActiveProfile.GamePath))
        {
            BackendStatus = new BackendStatusViewModel();
            return;
        }

        string gamePath = ActiveProfile.GamePath;
        // Ultimate ASI Loader uses dinput8.dll for GTA IV CE
        bool asiLoaderExists = File.Exists(Path.Combine(gamePath, "dinput8.dll"));

        // DXVK uses vulkan.dll (under FusionFix) or d3d9.dll (standalone)
        bool dxvkExists = File.Exists(Path.Combine(gamePath, "vulkan.dll")) || File.Exists(Path.Combine(gamePath, "d3d9.dll"));

        string pluginsDir = Path.Combine(gamePath, "plugins");
        bool fusionFixExists = false;
        if (Directory.Exists(pluginsDir))
        {
            fusionFixExists = File.Exists(Path.Combine(pluginsDir, "GTAIV.FusionFix.asi")) ||
                              File.Exists(Path.Combine(pluginsDir, "FusionFix.asi")) ||
                              Directory.GetFiles(pluginsDir, "*FusionFix*.asi").Any();
        }

        bool scriptHookExists = File.Exists(Path.Combine(gamePath, "ScriptHook.dll")) ||
                                File.Exists(Path.Combine(gamePath, "ScriptHookDotNet.dll")) ||
                                (Directory.Exists(pluginsDir) && Directory.GetFiles(pluginsDir, "*ScriptHook*.dll").Any()) ||
                                (Directory.Exists(Path.Combine(gamePath, "scripts")) && Directory.GetFiles(Path.Combine(gamePath, "scripts"), "*ScriptHook*.dll").Any());

        bool memBiterExists = File.Exists(Path.Combine(gamePath, "MemBiter.dll"));
        bool bassAudioExists = File.Exists(Path.Combine(gamePath, "bass.dll")) || File.Exists(Path.Combine(gamePath, "Bass.net.dll"));

        string ffVer = "Unknown";
        string asiVer = "Unknown";
        string dxvkVer = "Unknown";

        if (ActiveProfile != null)
        {
            if (ActiveProfile.ToolVersions.TryGetValue("FusionFix", out string? vff)) ffVer = vff;
            if (ActiveProfile.ToolVersions.TryGetValue("ASILoader", out string? vasi)) asiVer = vasi;
            if (ActiveProfile.ToolVersions.TryGetValue("DXVK", out string? vdxvk)) dxvkVer = vdxvk;
        }

        BackendStatus = new BackendStatusViewModel(asiLoaderExists, asiVer, fusionFixExists, ffVer, dxvkExists, dxvkVer, scriptHookExists, memBiterExists, bassAudioExists);

        // Fetch latest versions from GitHub in the background
        _ = FetchLatestToolVersionsAsync(BackendStatus);
    }

    private async Task FetchLatestToolVersionsAsync(BackendStatusViewModel statusVm)
    {
        try
        {
            var asiRelease = await _backendToolManager.GetLatestReleaseAsync("ThirteenAG", "Ultimate-ASI-Loader");
            statusVm.AsiLoaderLatest = asiRelease.TagName;
        }
        catch
        {
            statusVm.AsiLoaderLatest = "Unavailable";
        }

        try
        {
            var ffRelease = await _backendToolManager.GetLatestReleaseAsync("ThirteenAG", "GTAIV.EFLC.FusionFix");
            statusVm.FusionFixLatest = ffRelease.TagName;
        }
        catch
        {
            statusVm.FusionFixLatest = "Unavailable";
        }

        try
        {
            var dxvkRelease = await _backendToolManager.GetLatestReleaseAsync("doitsujin", "dxvk");
            statusVm.DxvkLatest = dxvkRelease.TagName;
        }
        catch
        {
            statusVm.DxvkLatest = "Unavailable";
        }
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
            Filter = "Supported Files (*.zip;*.rar;*.7z;*.asi)|*.zip;*.rar;*.7z;*.asi|Mod Archives (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z|ASI Plugins (*.asi)|*.asi|All Files (*.*)|*.*",
            Title = "Select Mod Files or Archives to Import",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            await ImportArchivesAsync(dialog.FileNames);
        }
    }

    private void OpenGameDirInExplorer()
    {
        if (ActiveProfile != null && !string.IsNullOrWhiteSpace(ActiveProfile.GamePath) && Directory.Exists(ActiveProfile.GamePath))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ActiveProfile.GamePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to open folder: {ex.Message}";
            }
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
        await ImportArchivesAsync(new[] { archivePath });
    }

    public async Task ImportArchivesAsync(IEnumerable<string> archivePaths)
    {
        if (ActiveProfile == null)
        {
            MessageBox.Show("Please select an active profile before importing mods.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var pathsList = archivePaths?.ToList();
        if (pathsList == null || !pathsList.Any()) return;

        IsBusy = true;
        int total = pathsList.Count;
        int successCount = 0;
        var errors = new List<string>();

        try
        {
            for (int i = 0; i < total; i++)
            {
                string archivePath = pathsList[i];
                string fileName = Path.GetFileName(archivePath);
                string ext = Path.GetExtension(archivePath).ToLowerInvariant();
                bool isAsi = ext == ".asi";

                StatusText = isAsi 
                    ? $"[{i + 1}/{total}] Importing ASI plugin '{fileName}'..."
                    : $"[{i + 1}/{total}] Extracting '{fileName}'...";

                try
                {
                    string displayName;
                    string version;
                    string description;
                    string finalModPath;
                    IReadOnlyList<ModFile> modFiles;
                    string compatibility;
                    System.Collections.Generic.List<string> tags;

                    if (isAsi)
                    {
                        var parsed = _metadataService.ParseArchiveFileName(fileName);
                        displayName = parsed.DisplayName;
                        version = "1.0.0";
                        try
                        {
                            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(archivePath);
                            if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                            {
                                version = versionInfo.FileVersion.Trim();
                            }
                            else if (!string.IsNullOrEmpty(versionInfo.ProductVersion))
                            {
                                version = versionInfo.ProductVersion.Trim();
                            }
                        }
                        catch { }

                        if (parsed.Version != null)
                        {
                            version = parsed.Version;
                        }

                        description = $"Imported ASI plugin: {fileName}";
                        compatibility = "CE-compatible";
                        tags = parsed.Tags.ToList();
                        if (!tags.Contains("ASI"))
                        {
                            tags.Add("ASI");
                        }

                        string cleanModName = string.Concat(displayName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "");
                        finalModPath = Path.Combine(_libraryDir, cleanModName);
                        if (Directory.Exists(finalModPath))
                        {
                            finalModPath += "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                        }

                        Directory.CreateDirectory(finalModPath);
                        string targetFilePath = Path.Combine(finalModPath, fileName);
                        File.Copy(archivePath, targetFilePath, true);

                        long size = new FileInfo(targetFilePath).Length;
                        modFiles = new List<ModFile> { new ModFile(fileName, size, null) };
                    }
                    else
                    {
                        // Parse temporary destination in library
                        string tempGuid = Guid.NewGuid().ToString("N");
                        string extractionTarget = Path.Combine(_libraryDir, tempGuid);

                        // Analyze archive structure first
                        var toolsContext = new InstalledToolsContext(
                            BackendStatus.AsiLoaderInstalled,
                            BackendStatus.FusionFixInstalled,
                            BackendStatus.ScriptHookInstalled
                        );
                        var report = await _modStructureAnalyzer.AnalyzeAsync(archivePath, toolsContext);

                        bool isLegacy = ActiveProfile.LastKnownVersion != null && !ActiveProfile.LastKnownVersion.IsCompleteEdition;
                        var preference = isLegacy ? VersionCompatibility.LegacyOnly : VersionCompatibility.CompleteEditionOnly;

                        // Extract with zip-slip protection (filter if it is a dual target mod)
                        if (report.IsDualTarget)
                        {
                            await _archiveHandler.ExtractAsync(archivePath, extractionTarget, report, preference);
                        }
                        else
                        {
                            await _archiveHandler.ExtractAsync(archivePath, extractionTarget);
                        }

                        // Promote mod root if it is nested inside subfolders
                        StatusText = $"[{i + 1}/{total}] Optimizing directory structure for '{fileName}'...";
                        await Task.Run(() => _archiveHandler.PromoteModRoot(extractionTarget));

                        StatusText = $"[{i + 1}/{total}] Analyzing compatibility and metadata for '{fileName}'...";
                        
                        // Scan folder
                        var metadata = _metadataService.ScanExtractedDirectory(extractionTarget, fileName);

                        // Ingest via ParseArchiveFileName
                        var parsed = _metadataService.ParseArchiveFileName(fileName);
                        displayName = parsed.DisplayName;
                        
                        // Respect parsed version from filename as fallback if readme did not yield one
                        version = parsed.Version ?? metadata.Version;
                        tags = parsed.Tags.ToList();

                        bool hasUpdateFolder = report.DetectedTargets.Any(t => t.Target == DeploymentTarget.UpdateFolder);
                        if (hasUpdateFolder)
                        {
                            if (!tags.Contains("FusionOverloader", StringComparer.OrdinalIgnoreCase))
                            {
                                tags.Add("FusionOverloader");
                            }

                            bool hasMore = report.DetectedTargets.Any(t => t.Target != DeploymentTarget.UpdateFolder) || report.IsDualTarget;
                            if (hasMore)
                            {
                                if (!tags.Contains("mixed", StringComparer.OrdinalIgnoreCase))
                                {
                                    tags.Add("mixed");
                                }
                            }
                        }

                        description = metadata.Description;
                        compatibility = metadata.Compatibility;

                        // Move extraction folder to a clean mod name folder
                        string cleanModName = string.Concat(displayName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "");
                        finalModPath = Path.Combine(_libraryDir, cleanModName);
                        if (Directory.Exists(finalModPath))
                        {
                            // Append suffix if mod name folder already exists
                            finalModPath += "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                        }

                        Directory.Move(extractionTarget, finalModPath);
                        modFiles = metadata.FileManifest.Select(f => new ModFile(f, new FileInfo(Path.Combine(finalModPath, f)).Length, null)).ToList();
                    }

                    // Build StagedMod record
                    var stagedMod = LibraryViewModel.ApplyDerivedLibraryTags(new StagedMod(
                        Id: Guid.NewGuid().ToString("N"),
                        Name: displayName,
                        Version: version,
                        Description: description,
                        LibraryPath: finalModPath,
                        Files: modFiles,
                        IsEnabled: false,
                        Compatibility: compatibility,
                        DisplayName: displayName,
                        Tags: tags
                    ));

                    // Add to library
                    var target = _loadOrderService.DetermineDeployTarget(stagedMod);
                    var vm = new ModViewModel(stagedMod, false, 99, target);
                    
                    if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            LibraryVM.LibraryMods.Add(vm);
                        });
                    }
                    else
                    {
                        LibraryVM.LibraryMods.Add(vm);
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"'{Path.GetFileName(archivePath)}': {ex.Message}");
                }
            }

            // Save library and refresh lists once after batch import completes
            if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LibraryVM.SaveLibrary();
                    RefreshActiveModsList();
                });
            }
            else
            {
                LibraryVM.SaveLibrary();
                RefreshActiveModsList();
            }

            if (errors.Any())
            {
                string summary = $"Import completed with errors. Successfully imported {successCount}/{total} mods.\n\nFailed imports:\n" + string.Join("\n", errors);
                StatusText = $"Import completed with {errors.Count} error(s)";
                MessageBox.Show(summary, "Import Completed with Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                StatusText = $"Successfully imported {successCount} mod(s)!";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetVramPreset(object? param)
    {
        if (param == null) return;
        if (int.TryParse(param.ToString(), out int vramMb))
        {
            GpuVramMb = vramMb;
        }
    }

    private void SetVehicleBudgetPreset(object? param)
    {
        if (param == null) return;
        if (int.TryParse(param.ToString(), out int budget))
        {
            FusionFixSettings.VehicleBudget = budget;
        }
    }

    private void AutoCalculateVehicleBudget()
    {
        if (ActiveProfile == null || string.IsNullOrWhiteSpace(ActiveProfile.GamePath))
        {
            MessageBox.Show("Please set a valid GTA IV Game Path first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        string vehiclesPath = Path.Combine(ActiveProfile.GamePath, "pc", "models", "cdimages", "vehicles.img");
        if (!File.Exists(vehiclesPath))
        {
            MessageBox.Show($"Could not locate vehicles.img at:\n{vehiclesPath}\n\nPlease check your game path configuration.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            long fileSizeBytes = new FileInfo(vehiclesPath).Length;
            // Round up to nearest 10,000,000 bytes
            long rounded = ((fileSizeBytes + 9999999) / 10000000) * 10000000;
            
            FusionFixSettings.VehicleBudget = (int)rounded;
            StatusText = $"Vehicle budget auto-calculated from vehicles.img size: {fileSizeBytes:N0} bytes -> rounded to {rounded:N0} bytes.";
            MessageBox.Show($"Found vehicles.img ({fileSizeBytes:N0} bytes).\n\nAutomatically calculated and set vehicle budget to {rounded:N0} bytes.", "Calculation Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error calculating vehicle budget: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
                var enabledVms = LibraryVM.LibraryMods.Where(m => ActiveProfile.EnabledModIds.Contains(m.Id)).ToList();
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

        using (new ProfilerBlock(_logger, "ApplyDeploymentAsync"))
        {
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
                var sortedEnabled = LibraryVM.LibraryMods
                    .Where(m => m.IsEnabled)
                    .OrderBy(m => m.Priority)
                    .ToList();

                // A. Validate structure of all enabled mods
                var validator = new UpdateFolderValidator();
                var allErrors = new List<string>();
                foreach (var vm in sortedEnabled)
                {
                    var issues = validator.Validate(vm.Model);
                    var errors = issues.Where(i => i.Severity == "Error").Select(i => $"[{vm.Name}] {i.Message}").ToList();
                    allErrors.AddRange(errors);
                }

                if (allErrors.Any())
                {
                    throw new InvalidOperationException($"Mod structure errors detected:\n\n{string.Join(Environment.NewLine, allErrors)}");
                }

                // B. Detect loose file conflicts
                var entries = sortedEnabled.Select(v => new LoadOrderEntry(v.Id, v.Target, v.Priority)).OrderBy(e => e.Priority).ToList();
                var currentLoadOrder = new LoadOrderModel(entries);
                var enabledModels = sortedEnabled.Select(v => v.Model).ToList();
                var conflictState = _conflictDetector.DetectConflicts(enabledModels, currentLoadOrder);

                // 1. Undeploy currently active links
                StatusText = "Clearing physical links...";
                foreach (var modVm in LibraryVM.LibraryMods)
                {
                    await adapter.UndeployAsync(modVm.Model);
                }

                // 2. Deploy enabled links in load order priority sequence
                StatusText = "Applying junctions and hard links...";
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
    }

    internal void SaveProfileState(Profile profile)
    {
        string file = Path.Combine(_profilesDir, $"{profile.Id}.json");
        _profileManager.SaveProfile(file, profile);
        
        // Sync collection
        var existing = Profiles.FirstOrDefault(p => p.Id == profile.Id);
        if (existing != null)
        {
            Profiles[Profiles.IndexOf(existing)] = profile;
        }
        _activeProfile = profile;
        OnPropertyChanged(nameof(ActiveProfile));
        OnPropertyChanged(nameof(GpuVramMb));
    }

    private async void RemoveActiveProfile()
    {
        if (ActiveProfile == null) return;
        if (Profiles.Count <= 1)
        {
            MessageBox.Show("Cannot delete the only remaining profile. Please create another profile first.", "Cannot Remove Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var profileToDelete = ActiveProfile;
        var result = MessageBox.Show(
            $"Are you sure you want to delete the profile '{profileToDelete.Name}'?\n\nThis will remove the configuration profile, but will not delete your actual mod library files.",
            "Confirm Profile Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // First, undeploy current profile links if it's active
            IsBusy = true;
            StatusText = "Tearing down profile junctions...";
            try
            {
                var journal = new TransactionJournal();
                var adapter = new CompleteEditionAdapter(profileToDelete.GamePath, _linker, journal);
                var enabledVms = LibraryVM.LibraryMods.Where(m => profileToDelete.EnabledModIds.Contains(m.Id)).ToList();
                await Task.WhenAll(enabledVms.Select(vm => adapter.UndeployAsync(vm.Model)));
            }
            catch (Exception ex)
            {
                StatusText = $"Teardown warning: {ex.Message}";
            }

            // Delete the file
            string file = Path.Combine(_profilesDir, $"{profileToDelete.Id}.json");
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete profile file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    IsBusy = false;
                    return;
                }
            }

            Profiles.Remove(profileToDelete);
            ActiveProfile = Profiles.First();
            IsBusy = false;
            StatusText = $"Deleted profile '{profileToDelete.Name}' and switched to '{ActiveProfile.Name}'.";
        }
    }

    private void RenameActiveProfile(string? newName)
    {
        if (ActiveProfile == null || string.IsNullOrWhiteSpace(newName)) return;

        string oldName = ActiveProfile.Name;
        if (oldName == newName) return;

        var updatedProfile = ActiveProfile with { Name = newName };
        SaveProfileState(updatedProfile);
        StatusText = $"Renamed profile from '{oldName}' to '{newName}'.";
    }

    private async Task DeleteModAsync(ModViewModel? modVm)
    {
        if (modVm == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to permanently delete the mod '{modVm.Name}' from the library?\n\nThis will remove it from all profiles and delete its files from your disk.",
            "Delete Mod",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.No)
        {
            return;
        }

        IsBusy = true;
        StatusText = $"Deleting mod '{modVm.Name}'...";

        try
        {
            // 1. Physically undeploy it from the game directory if game path is configured
            if (ActiveProfile != null && !string.IsNullOrEmpty(ActiveProfile.GamePath) && Directory.Exists(ActiveProfile.GamePath))
            {
                var adapter = new CompleteEditionAdapter(ActiveProfile.GamePath, _linker);
                await adapter.UndeployAsync(modVm.Model);
            }

            // 2. Remove the mod from all profiles
            var profilesList = Profiles.ToList();
            foreach (var profile in profilesList)
            {
                bool profileChanged = false;
                var enabledIds = profile.EnabledModIds.ToList();
                if (enabledIds.Contains(modVm.Id))
                {
                    enabledIds.Remove(modVm.Id);
                    profileChanged = true;
                }

                var loadOrderEntries = profile.LoadOrder.Entries.Where(e => e.ModId != modVm.Id).ToList();
                if (loadOrderEntries.Count != profile.LoadOrder.Entries.Count)
                {
                    profileChanged = true;
                }

                if (profileChanged)
                {
                    var updatedProfile = profile with {
                        EnabledModIds = enabledIds,
                        LoadOrder = new LoadOrderModel(loadOrderEntries)
                    };
                    SaveProfileState(updatedProfile);
                }
            }

            // 3. Remove from library list and save library manifest
            if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LibraryVM.LibraryMods.Remove(modVm);
                    if (SelectedMod == modVm) SelectedMod = null;
                    if (LibraryVM.SelectedLibraryMod == modVm) LibraryVM.SelectedLibraryMod = null;
                    if (LibraryVM.SelectedPluginMod == modVm) LibraryVM.SelectedPluginMod = null;
                });
            }
            else
            {
                LibraryVM.LibraryMods.Remove(modVm);
                if (SelectedMod == modVm) SelectedMod = null;
                if (LibraryVM.SelectedLibraryMod == modVm) LibraryVM.SelectedLibraryMod = null;
                if (LibraryVM.SelectedPluginMod == modVm) LibraryVM.SelectedPluginMod = null;
            }
            LibraryVM.SaveLibrary();

            // 4. Delete the physical directory
            if (Directory.Exists(modVm.Model.LibraryPath))
            {
                await Task.Run(() => Directory.Delete(modVm.Model.LibraryPath, true));
            }

            // 5. Refresh active profile links list
            RefreshActiveModsList();

            StatusText = $"Deleted mod '{modVm.Name}' successfully.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to delete mod: {ex.Message}";
            MessageBox.Show($"Failed to delete mod: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            UpdateConflictsAndWatchdog();
        }
    }

    private async Task ResetGameDirectoryAsync()
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath) || !Directory.Exists(ActiveProfile.GamePath))
        {
            MessageBox.Show("Please select a valid game directory first.", "Cannot Reset", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            "Are you sure you want to restore the game directory to its clean vanilla structure?\n\nThis will:\n" +
            "- Undeploy all active mods (remove junctions and links)\n" +
            "- Uninstall all tools (FusionFix, Ultimate ASI Loader, DXVK)\n" +
            "- Remove the 'update', 'plugins', and 'scripts' directories (if empty)\n" +
            "- Remove 'dxvk.conf' and 'commandline.txt' if present\n\n" +
            "No original game files will be modified. This action cannot be undone.",
            "Confirm Reset to Clean Game Structure",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        StatusText = "Restoring vanilla game structure...";

        try
        {
            // Backup settings before we reset/wipe the directory
            BackupSettings();

            // 1. Undeploy all mods first from the current active game path
            var adapter = new CompleteEditionAdapter(ActiveProfile.GamePath, _linker);
            foreach (var modVm in LibraryVM.LibraryMods)
            {
                try
                {
                    await adapter.UndeployAsync(modVm.Model);
                }
                catch { /* Ignore individual undeploy errors */ }
            }

            // 2. Load and clean all tools tracked by the manifest
            var manifest = await LoadToolsManifestAsync();
            if (manifest != null && manifest.Count > 0)
            {
                foreach (var file in manifest)
                {
                    if (File.Exists(file.InstalledPath))
                    {
                        try { File.Delete(file.InstalledPath); } catch { }
                    }
                }
            }

            // 3. Fallback/Thoroughness: Delete default files for all tools
            var allTools = new[] { "FusionFix", "ASILoader", "DXVK" };
            foreach (var tool in allTools)
            {
                var defaultFiles = GetDefaultFilesForTool(tool);
                foreach (var relPath in defaultFiles)
                {
                    string absPath = Path.Combine(ActiveProfile.GamePath, relPath);
                    if (File.Exists(absPath))
                    {
                        try { File.Delete(absPath); } catch { }
                    }
                }
            }

            // 4. Delete the log files if they exist
            var logPaths = adapter.BackendLogPaths();
            foreach (var logPath in logPaths)
            {
                if (File.Exists(logPath))
                {
                    try { File.Delete(logPath); } catch { }
                }
            }

            // 5. Delete empty folders/structures that are part of mod loader but not vanilla
            string[] dirsToDelete = new[]
            {
                Path.Combine(ActiveProfile.GamePath, "update"),
                Path.Combine(ActiveProfile.GamePath, "plugins"),
                Path.Combine(ActiveProfile.GamePath, "scripts")
            };
            foreach (var dir in dirsToDelete)
            {
                if (Directory.Exists(dir))
                {
                    try { Directory.Delete(dir, recursive: true); } catch { }
                }
            }

            // 6. Delete the tools manifest file itself
            string manifestPath = Path.Combine(_profilesDir, $"{ActiveProfile.Id}_tools_manifest.json");
            if (File.Exists(manifestPath))
            {
                try { File.Delete(manifestPath); } catch { }
            }

            // 7. Clear tool installation versions in the profile
            var updatedProfile = ActiveProfile with { InstalledToolVersions = new Dictionary<string, string>() };
            _profileManager.SaveProfile(Path.Combine(_profilesDir, $"{updatedProfile.Id}.json"), updatedProfile);
            ActiveProfile = updatedProfile;

            StatusText = "Restored vanilla game structure successfully.";
            MessageBox.Show("The game directory has been successfully restored to its original clean state.", "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText = $"Reset failed: {ex.Message}";
            MessageBox.Show($"Failed to reset game directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            UpdateBackendStatus();
            UpdateConflictsAndWatchdog();
        }
    }

    private string GetDxvkConfPath()
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath)) return "";
        return Path.Combine(ActiveProfile.GamePath, "dxvk.conf");
    }

    private string GetFusionFixIniPath()
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath)) return "";
        string path1 = Path.Combine(ActiveProfile.GamePath, "plugins", "GTAIV.EFLC.FusionFix.ini");
        string path2 = Path.Combine(ActiveProfile.GamePath, "plugins", "FusionFix.ini");
        if (File.Exists(path1)) return path1;
        if (File.Exists(path2)) return path2;
        return path1;
    }

    private void LoadFusionFixConfig()
    {
        string iniPath = GetFusionFixIniPath();
        if (File.Exists(iniPath))
        {
            FusionFixSettings = FusionFixConfig.Load(iniPath);
            IsFusionFixConfigAvailable = true;
        }
        else
        {
            FusionFixSettings = new FusionFixConfig();
            IsFusionFixConfigAvailable = false;
        }

        string dxvkPath = GetDxvkConfPath();
        if (File.Exists(dxvkPath))
        {
            DxvkSettings = DxvkConfig.Load(dxvkPath);
            IsDxvkConfigAvailable = true;
        }
        else
        {
            DxvkSettings = new DxvkConfig();
            IsDxvkConfigAvailable = false;
        }
    }

    private void SaveFusionFixConfig()
    {
        string iniPath = GetFusionFixIniPath();
        string dxvkPath = GetDxvkConfPath();

        bool ffSaved = false;
        bool dxvkSaved = false;

        if (IsFusionFixConfigAvailable && !string.IsNullOrEmpty(iniPath) && File.Exists(iniPath))
        {
            try
            {
                FusionFixConfig.Save(iniPath, FusionFixSettings);
                ffSaved = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save FusionFix configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        if (IsDxvkConfigAvailable && !string.IsNullOrEmpty(dxvkPath) && File.Exists(dxvkPath))
        {
            try
            {
                DxvkConfig.Save(dxvkPath, DxvkSettings);
                dxvkSaved = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save DXVK configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        if (ffSaved || dxvkSaved)
        {
            StatusText = "Backend configuration saved successfully.";
            MessageBox.Show("Backend configuration saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("No configuration files found to save. Install FusionFix or DXVK first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadDefaults()
    {
        if (IsFusionFixTabActive)
        {
            LoadFusionFixDefaultsInternal(showDialogs: true);
        }
        else
        {
            LoadDxvkDefaultsInternal(showDialogs: true);
        }
    }

    public void LoadDxvkDefaultsInternal(bool showDialogs)
    {
        string defaultConfPath = Path.Combine(_baseDir, "dxvkDefault.conf");
        if (File.Exists(defaultConfPath))
        {
            try
            {
                DxvkSettings = DxvkConfig.Load(defaultConfPath);
                StatusText = "Restored default settings from installed DXVK package.";
                if (showDialogs)
                {
                    MessageBox.Show("DXVK default configuration loaded from the installation package. Click 'Save Configuration' to apply changes to the active game profile.", "Defaults Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (showDialogs)
                {
                    MessageBox.Show($"Failed to load DXVK defaults: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    throw;
                }
            }
        }
        else
        {
            DxvkSettings = new DxvkConfig();
            StatusText = "Restored built-in default DXVK settings.";
            if (showDialogs)
            {
                MessageBox.Show("No default configuration file from installation package was found. Restored built-in defaults instead. Click 'Save Configuration' to apply changes to the active game profile.", "Built-in Defaults Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void RefreshFusionFixConfig()
    {
        LoadFusionFixConfig();
        StatusText = "FusionFix configuration reloaded successfully.";
    }

    public void LoadFusionFixDefaultsInternal(bool showDialogs)
    {
        string defaultIniPath = Path.Combine(_baseDir, "FusionFixDefault.ini");
        if (File.Exists(defaultIniPath))
        {
            try
            {
                FusionFixSettings = FusionFixConfig.Load(defaultIniPath);
                StatusText = "Restored default settings from installed FusionFix package.";
                if (showDialogs)
                {
                    MessageBox.Show("FusionFix default configuration loaded from the installation package. Click 'Save Configuration' to apply changes to the active game profile.", "Defaults Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (showDialogs)
                {
                    MessageBox.Show($"Failed to load FusionFix defaults: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    throw;
                }
            }
        }
        else
        {
            FusionFixSettings = new FusionFixConfig();
            StatusText = "Restored built-in default settings.";
            if (showDialogs)
            {
                MessageBox.Show("No default configuration file from installation package was found. Restored built-in defaults instead. Click 'Save Configuration' to apply changes to the active game profile.", "Built-in Defaults Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void LoadSaveProfilesData()
    {
        BaseProfileIds.Clear();
        var bases = _saveProfileManager.GetBaseProfileIds();
        foreach (var b in bases)
        {
            BaseProfileIds.Add(b);
        }
        SelectedBaseProfileId = BaseProfileIds.FirstOrDefault();
        OnPropertyChanged(nameof(HasBaseProfiles));
    }

    private void RefreshSaveProfilesList()
    {
        SaveProfiles.Clear();
        if (string.IsNullOrEmpty(SelectedBaseProfileId)) return;

        var list = _saveProfileManager.GetSaveProfiles(SelectedBaseProfileId);
        foreach (var sp in list)
        {
            SaveProfiles.Add(sp);
        }
    }

    private void BrowseSaveProfilesPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Rockstar Games GTA IV Profiles Directory",
            InitialDirectory = Directory.Exists(GtaSaveProfilesPath) ? GtaSaveProfilesPath : null
        };

        if (dialog.ShowDialog() == true)
        {
            GtaSaveProfilesPath = dialog.FolderName;
        }
    }

    private void ActivateSaveProfile(object? param)
    {
        var targetProfile = param as SaveProfile ?? SelectedSaveProfile;
        if (targetProfile == null || string.IsNullOrEmpty(SelectedBaseProfileId)) return;

        try
        {
            _saveProfileManager.ActivateSaveProfile(SelectedBaseProfileId, targetProfile, string.Empty);
            RenameActiveSaveTo = "";
            RefreshSaveProfilesList();
            StatusText = $"Activated save profile: '{targetProfile.DisplayName}'";
            MessageBox.Show($"Activated save profile '{targetProfile.DisplayName}' successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to activate save profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void QuickSnapshotSaveProfile()
    {
        if (string.IsNullOrEmpty(SelectedBaseProfileId)) return;

        try
        {
            string name = string.IsNullOrWhiteSpace(QuickSnapshotName)
                ? $"Safety_Snapshot_{DateTime.Now:yyyyMMdd_HHmmss}"
                : QuickSnapshotName;

            var snapshot = _saveProfileManager.CloneActiveSaveProfile(SelectedBaseProfileId, name);
            QuickSnapshotName = "";
            RefreshSaveProfilesList();
            StatusText = $"Created safety snapshot '{snapshot.DisplayName}' successfully.";
            MessageBox.Show($"Created safety snapshot '{snapshot.DisplayName}' successfully.", "Instant Snapshot Created", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create snapshot: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenSaveProfileFolder(SaveProfile? saveProfile)
    {
        var target = saveProfile ?? SelectedSaveProfile;
        if (target != null && Directory.Exists(target.FullPath))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = target.FullPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to open save directory: {ex.Message}";
            }
        }
    }

    private void CreateSaveProfile()
    {
        if (string.IsNullOrEmpty(SelectedBaseProfileId)) return;

        if (string.IsNullOrWhiteSpace(NewSaveProfileName))
        {
            MessageBox.Show("Please enter a name for the new save profile.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _saveProfileManager.CreateNewSaveProfile(SelectedBaseProfileId, NewSaveProfileName, RenameActiveSaveTo);
            NewSaveProfileName = "";
            RenameActiveSaveTo = "";
            RefreshSaveProfilesList();
            StatusText = $"Created new save profile successfully.";
            MessageBox.Show($"Created new save profile successfully. The game will start a new story in this save folder.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create save profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RenameSaveProfile(string? newName)
    {
        if (SelectedSaveProfile == null || string.IsNullOrWhiteSpace(newName)) return;

        try
        {
            _saveProfileManager.RenameSaveProfile(SelectedSaveProfile, newName);
            RefreshSaveProfilesList();
            StatusText = $"Renamed save profile to '{newName}'";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to rename save profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteSaveProfile()
    {
        if (SelectedSaveProfile == null) return;

        if (SelectedSaveProfile.IsActive)
        {
            MessageBox.Show("Cannot delete the currently active save profile directory. Please activate another save first.", "Cannot Delete Active Save", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to permanently delete the save profile '{SelectedSaveProfile.DisplayName}'?\n\nThis physical directory will be deleted from your disk.",
            "Confirm Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _saveProfileManager.DeleteSaveProfile(SelectedSaveProfile);
                RefreshSaveProfilesList();
                StatusText = "Deleted save profile.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete save profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ImportSaveFile(object? param)
    {
        if (string.IsNullOrEmpty(SelectedBaseProfileId))
        {
            MessageBox.Show("Please select a base profile first.", "Cannot Import", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string targetFileName = param as string ?? "SGTA401";

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "GTA IV Save Files (SGTA40*)|SGTA*|All Files (*.*)|*.*",
            Title = "Select GTA IV Save File to Import"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string sourceFile = dialog.FileName;
                string activePath = Path.Combine(_saveProfileManager.ProfilesPath, SelectedBaseProfileId);
                
                if (!Directory.Exists(activePath))
                {
                    Directory.CreateDirectory(activePath);
                }

                string selectedFileName = Path.GetFileName(sourceFile);
                string destinationFileName = targetFileName;
                
                if (selectedFileName.StartsWith("SGTA", StringComparison.OrdinalIgnoreCase) && selectedFileName.Length == 7 && char.IsDigit(selectedFileName[4]) && char.IsDigit(selectedFileName[5]) && char.IsDigit(selectedFileName[6]))
                {
                    var slotCodeStr = selectedFileName.Substring(4, 3);
                    if (int.TryParse(slotCodeStr, out int slotCode))
                    {
                        int slotNum = slotCode - 400;
                        string episode = "GTA IV";
                        if (slotCode >= 413 && slotCode <= 424)
                        {
                            episode = "TLAD";
                            slotNum = slotCode - 412;
                        }
                        else if (slotCode >= 425 && slotCode <= 436)
                        {
                            episode = "TBOGT";
                            slotNum = slotCode - 424;
                        }

                        var askResult = MessageBox.Show(
                            $"The selected file '{selectedFileName}' matches {episode} Slot {slotNum}.\n\n" +
                            $"Would you like to import it as '{selectedFileName}' ({episode} Slot {slotNum})?\n" +
                            $"Click 'No' to import it into your selected slot (Slot {targetFileName.Substring(4)}) instead.",
                            "Detect Save Slot",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Question);

                        if (askResult == MessageBoxResult.Cancel)
                        {
                            return;
                        }
                        else if (askResult == MessageBoxResult.Yes)
                        {
                            destinationFileName = selectedFileName;
                        }
                    }
                }

                string destinationPath = Path.Combine(activePath, destinationFileName);

                if (File.Exists(destinationPath))
                {
                    int finalSlotCode = int.Parse(destinationFileName.Substring(4));
                    int finalSlotNum = finalSlotCode - 400;
                    string finalEpisode = "GTA IV";
                    if (finalSlotCode >= 413 && finalSlotCode <= 424)
                    {
                        finalEpisode = "TLAD";
                        finalSlotNum = finalSlotCode - 412;
                    }
                    else if (finalSlotCode >= 425 && finalSlotCode <= 436)
                    {
                        finalEpisode = "TBOGT";
                        finalSlotNum = finalSlotCode - 424;
                    }

                    var overwriteResult = MessageBox.Show(
                        $"A save file already exists in slot {finalEpisode} Slot {finalSlotNum} ({destinationFileName}).\n\n" +
                        "Are you sure you want to overwrite it? This cannot be undone.",
                        "Overwrite Save File",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (overwriteResult == MessageBoxResult.No)
                    {
                        return;
                    }
                }

                File.Copy(sourceFile, destinationPath, overwrite: true);

                RefreshSaveProfilesList();
                StatusText = $"Successfully imported save file to active {destinationFileName}";
                
                int resultSlotCode = int.Parse(destinationFileName.Substring(4));
                int resultSlotNum = resultSlotCode - 400;
                string resultEpisode = "GTA IV";
                if (resultSlotCode >= 413 && resultSlotCode <= 424)
                {
                    resultEpisode = "TLAD";
                    resultSlotNum = resultSlotCode - 412;
                }
                else if (resultSlotCode >= 425 && resultSlotCode <= 436)
                {
                    resultEpisode = "TBOGT";
                    resultSlotNum = resultSlotCode - 424;
                }

                MessageBox.Show($"Successfully imported save file as {destinationFileName} ({resultEpisode} Slot {resultSlotNum}).", "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import save file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ApplyTheme(bool isDark)
    {
        isDark = true;
        try
        {
            if (isDark)
            {
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
                Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(System.Windows.Media.Color.FromArgb(255, 255, 165, 0), Wpf.Ui.Appearance.ApplicationTheme.Dark);
                ApplyCustomPalette(true);
            }
            else
            {
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
                Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(System.Windows.Media.Color.FromArgb(255, 230, 145, 0), Wpf.Ui.Appearance.ApplicationTheme.Light);
                ApplyCustomPalette(false);
            }
        }
        catch
        {
            // Ignore if theme dictionaries are not fully initialized yet
        }
    }

    private void ApplyCustomPalette(bool isDark)
    {
        try
        {
            var resources = Application.Current.Resources;

            if (isDark)
            {
                // Dark Theme: Liberty City Nights
                // Neutral layering model: Base canvas < Sidebar < Surface (controls) < Elevated (cards).
                SetResourceBrush(resources, "ApplicationBackgroundBrush", "#FF0D0D0D");
                SetResourceBrush(resources, "SolidBackgroundFillColorBaseBrush", "#FF0D0D0D");

                SetResourceBrush(resources, "MicaBackgroundBrush", "#FF121212");
                SetResourceBrush(resources, "SolidBackgroundFillColorSecondaryBrush", "#FF141414"); // Sidebar canvas
                SetResourceBrush(resources, "SolidBackgroundFillColorTertiaryBrush", "#FF1A1A1A");
                SetResourceBrush(resources, "ControlFillColorDefaultBrush", "#FF1A1A1A");           // Surface: inputs/buttons
                SetResourceBrush(resources, "CardBackgroundFillColorDefaultBrush", "#FF242424");     // Elevated surface: cards

                SetResourceBrush(resources, "ControlStrokeColorDefaultBrush", "#FF2D2D30");
                SetResourceBrush(resources, "ControlElevationBorderBrush", "#FF2D2D30");
                SetResourceBrush(resources, "CardStrokeColorDefaultBrush", "#FF3A3A3C");
                SetResourceBrush(resources, "TextFillColorDisabledBrush", "#FF2D2D30");

                // Typography layering
                SetResourceBrush(resources, "TextFillColorPrimaryBrush", "#FFFFFFFF");
                SetResourceBrush(resources, "TextFillColorSecondaryBrush", "#FFA1A1AA");
                SetResourceBrush(resources, "TextFillColorTertiaryBrush", "#FF71717A");

                // Accents: Primary (#FFFA9600), Secondary/Hover (#FFFFB347)
                SetResourceColor(resources, "SystemAccentColor", "#FFFA9600");
                SetResourceBrush(resources, "SystemAccentBrush", "#FFFA9600");
                SetResourceColor(resources, "SystemAccentColorPrimary", "#FFFA9600");
                SetResourceBrush(resources, "SystemAccentColorPrimaryBrush", "#FFFA9600");

                SetResourceColor(resources, "SystemAccentColorSecondary", "#FFFFB347");
                SetResourceBrush(resources, "SystemAccentColorSecondaryBrush", "#FFFFB347");
                SetResourceColor(resources, "SystemAccentColorTertiary", "#FFFFB347");
                SetResourceBrush(resources, "SystemAccentColorTertiaryBrush", "#FFFFB347");

                SetResourceColor(resources, "SystemAccentColorLight1", "#FFFFB347");
                SetResourceBrush(resources, "SystemAccentColorLight1Brush", "#FFFFB347");
                SetResourceColor(resources, "SystemAccentColorLight2", "#FFFFC773");
                SetResourceBrush(resources, "SystemAccentColorLight2Brush", "#FFFFC773");
                SetResourceColor(resources, "SystemAccentColorLight3", "#FFFFDBA6");
                SetResourceBrush(resources, "SystemAccentColorLight3Brush", "#FFFFDBA6");
                SetResourceColor(resources, "SystemAccentColorDark1", "#FFD47A00");
                SetResourceBrush(resources, "SystemAccentColorDark1Brush", "#FFD47A00");
                SetResourceColor(resources, "SystemAccentColorDark2", "#FFA85F00");
                SetResourceBrush(resources, "SystemAccentColorDark2Brush", "#FFA85F00");
                SetResourceColor(resources, "SystemAccentColorDark3", "#FF7A4500");
                SetResourceBrush(resources, "SystemAccentColorDark3Brush", "#FF7A4500");

                // Muted accent: active tag/selection backgrounds + subtle highlights
                SetResourceBrush(resources, "MutedAccentBrush", "#FF3A2E24");
                SetResourceBrush(resources, "WarningTagTextBrush", "#FFFFB347");

                // Semantic status (override WPF-UI fill keys so XAML stays on default names)
                SetResourceColor(resources, "SystemFillColorSuccess", "#FF2E7D32");
                SetResourceBrush(resources, "SystemFillColorSuccessBrush", "#FF2E7D32");
                SetResourceColor(resources, "SystemFillColorCaution", "#FFED6C02");
                SetResourceBrush(resources, "SystemFillColorCautionBrush", "#FFED6C02");
                SetResourceColor(resources, "SystemFillColorCritical", "#FFD32F2F");
                SetResourceBrush(resources, "SystemFillColorCriticalBrush", "#FFD32F2F");

                // Legacy aliases
                SetResourceColor(resources, "SystemGreenColor", "#FF2E7D32");
                SetResourceBrush(resources, "SystemGreenBrush", "#FF2E7D32");
                SetResourceColor(resources, "SystemRedColor", "#FFD32F2F");
                SetResourceBrush(resources, "SystemRedBrush", "#FFD32F2F");
                SetResourceBrush(resources, "WarmAccentBrush", "#FF3A2E24");
                SetResourceBrush(resources, "SecondaryAccentBrush", "#FFFFB347");
            }
            else
            {
                // Light Theme: Algonquin Daylight
                // Soft gray layers reduce glare; elevated cards sit a touch below the white surface.
                SetResourceBrush(resources, "ApplicationBackgroundBrush", "#FFF5F5F7");
                SetResourceBrush(resources, "SolidBackgroundFillColorBaseBrush", "#FFF5F5F7");

                SetResourceBrush(resources, "MicaBackgroundBrush", "#FFEAEAEA");
                SetResourceBrush(resources, "SolidBackgroundFillColorSecondaryBrush", "#FFFFFFFF"); // Sidebar canvas
                SetResourceBrush(resources, "SolidBackgroundFillColorTertiaryBrush", "#FFFAFAFA");
                SetResourceBrush(resources, "ControlFillColorDefaultBrush", "#FFFFFFFF");           // Surface: inputs/buttons
                SetResourceBrush(resources, "CardBackgroundFillColorDefaultBrush", "#FFFAFAFA");     // Elevated surface: cards

                SetResourceBrush(resources, "ControlStrokeColorDefaultBrush", "#FFE5E5EA");
                SetResourceBrush(resources, "ControlElevationBorderBrush", "#FFE5E5EA");
                SetResourceBrush(resources, "CardStrokeColorDefaultBrush", "#FFD1D1D6");
                SetResourceBrush(resources, "TextFillColorDisabledBrush", "#FFC7C7CC");

                // Typography layering
                SetResourceBrush(resources, "TextFillColorPrimaryBrush", "#FF1C1C1E");
                SetResourceBrush(resources, "TextFillColorSecondaryBrush", "#FF636366");
                SetResourceBrush(resources, "TextFillColorTertiaryBrush", "#FF8E8E93");

                // Accents: Primary (#FFD47A00), Secondary/Hover (#FFE68500)
                SetResourceColor(resources, "SystemAccentColor", "#FFD47A00");
                SetResourceBrush(resources, "SystemAccentBrush", "#FFD47A00");
                SetResourceColor(resources, "SystemAccentColorPrimary", "#FFD47A00");
                SetResourceBrush(resources, "SystemAccentColorPrimaryBrush", "#FFD47A00");

                SetResourceColor(resources, "SystemAccentColorSecondary", "#FFE68500");
                SetResourceBrush(resources, "SystemAccentColorSecondaryBrush", "#FFE68500");
                SetResourceColor(resources, "SystemAccentColorTertiary", "#FFE68500");
                SetResourceBrush(resources, "SystemAccentColorTertiaryBrush", "#FFE68500");

                SetResourceColor(resources, "SystemAccentColorLight1", "#FFE68500");
                SetResourceBrush(resources, "SystemAccentColorLight1Brush", "#FFE68500");
                SetResourceColor(resources, "SystemAccentColorLight2", "#FFF59A1F");
                SetResourceBrush(resources, "SystemAccentColorLight2Brush", "#FFF59A1F");
                SetResourceColor(resources, "SystemAccentColorLight3", "#FFFFB347");
                SetResourceBrush(resources, "SystemAccentColorLight3Brush", "#FFFFB347");
                SetResourceColor(resources, "SystemAccentColorDark1", "#FFB86A00");
                SetResourceBrush(resources, "SystemAccentColorDark1Brush", "#FFB86A00");
                SetResourceColor(resources, "SystemAccentColorDark2", "#FF945500");
                SetResourceBrush(resources, "SystemAccentColorDark2Brush", "#FF945500");
                SetResourceColor(resources, "SystemAccentColorDark3", "#FF6E3F00");
                SetResourceBrush(resources, "SystemAccentColorDark3Brush", "#FF6E3F00");

                // Muted accent: active tag/selection backgrounds + subtle highlights
                SetResourceBrush(resources, "MutedAccentBrush", "#FFFDF3E6");
                SetResourceBrush(resources, "WarningTagTextBrush", "#FFB25E00");

                // Semantic status (override WPF-UI fill keys so XAML stays on default names)
                SetResourceColor(resources, "SystemFillColorSuccess", "#FF2E7D32");
                SetResourceBrush(resources, "SystemFillColorSuccessBrush", "#FF2E7D32");
                SetResourceColor(resources, "SystemFillColorCaution", "#FFE65100");
                SetResourceBrush(resources, "SystemFillColorCautionBrush", "#FFE65100");
                SetResourceColor(resources, "SystemFillColorCritical", "#FFC62828");
                SetResourceBrush(resources, "SystemFillColorCriticalBrush", "#FFC62828");

                // Legacy aliases
                SetResourceColor(resources, "SystemGreenColor", "#FF2E7D32");
                SetResourceBrush(resources, "SystemGreenBrush", "#FF2E7D32");
                SetResourceColor(resources, "SystemRedColor", "#FFC62828");
                SetResourceBrush(resources, "SystemRedBrush", "#FFC62828");
                SetResourceBrush(resources, "WarmAccentBrush", "#FFFDF3E6");
                SetResourceBrush(resources, "SecondaryAccentBrush", "#FFE68500");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply custom palette (isDark={isDark}): {ex.Message}");
        }
    }

    private void SetResourceBrush(ResourceDictionary resources, string key, string hexColor)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hexColor);
            if (resources.Contains(key))
            {
                if (resources[key] is SolidColorBrush brush)
                {
                    if (brush.Color != color)
                    {
                        if (brush.IsFrozen)
                        {
                            resources[key] = new SolidColorBrush(color);
                        }
                        else
                        {
                            brush.Color = color;
                        }
                    }
                }
                else
                {
                    resources[key] = new SolidColorBrush(color);
                }
            }
            else
            {
                resources.Add(key, new SolidColorBrush(color));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set resource brush '{key}' ({hexColor}): {ex.Message}");
        }
    }

    private void SetResourceColor(ResourceDictionary resources, string key, string hexColor)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hexColor);
            resources[key] = color;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set resource color '{key}' ({hexColor}): {ex.Message}");
        }
    }

    private void SaveSettings()
    {
        try
        {
            string settingsFile = Path.Combine(_baseDir, "settings.json");
            var settings = new AppSettings 
            { 
                IsDarkTheme = _isDarkTheme,
                GtaSaveProfilesPath = _gtaSaveProfilesPath
            };
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsFile, json);
        }
        catch
        {
            // Ignore settings save errors
        }
    }

    private async Task InstallFusionFixAsync()
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath) || !Directory.Exists(ActiveProfile.GamePath))
        {
            MessageBox.Show("Please select a valid game directory first.", "Cannot Install", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        StatusText = "Querying latest FusionFix release from GitHub...";

        try
        {
            var release = await _backendToolManager.GetLatestReleaseAsync("ThirteenAG", "GTAIV.EFLC.FusionFix");
            if (release.Assets == null || !release.Assets.ContainsKey("GTAIV.EFLC.FusionFix.zip"))
            {
                throw new Exception("Could not find standard FusionFix asset in the latest release.");
            }

            string downloadUrl = release.Assets["GTAIV.EFLC.FusionFix.zip"];
            string cacheZip = Path.Combine(_baseDir, "Cache", "GTAIV.EFLC.FusionFix.zip");

            StatusText = "Downloading GTAIV.EFLC.FusionFix.zip...";
            await _backendToolManager.DownloadToolAsync(downloadUrl, cacheZip);

            string tempExtractionDir = Path.Combine(_baseDir, "Cache", "FusionFixTemp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempExtractionDir);

            StatusText = "Extracting FusionFix...";
            await _archiveHandler.ExtractAsync(cacheZip, tempExtractionDir);

            // If it's legacy version, we also download the Legacy Addon
            bool isLegacy = ActiveProfile.LastKnownVersion != null && !ActiveProfile.LastKnownVersion.IsCompleteEdition;
            if (isLegacy)
            {
                if (release.Assets.ContainsKey("GTAIV.EFLC.FusionFixLegacyAddon.zip"))
                {
                    string legacyDownloadUrl = release.Assets["GTAIV.EFLC.FusionFixLegacyAddon.zip"];
                    string legacyCacheZip = Path.Combine(_baseDir, "Cache", "GTAIV.EFLC.FusionFixLegacyAddon.zip");

                    StatusText = "Downloading GTAIV.EFLC.FusionFixLegacyAddon.zip...";
                    await _backendToolManager.DownloadToolAsync(legacyDownloadUrl, legacyCacheZip);

                    StatusText = "Extracting Legacy Addon...";
                    await _archiveHandler.ExtractAsync(legacyCacheZip, tempExtractionDir);
                }
                else
                {
                    StatusText = "Warning: Legacy version detected but no legacy addon found in release. Proceeding with standard install...";
                }
            }

            // Cache the default .ini from the extracted folder for "Defaults" configuration restore
            try
            {
                string? defaultIniSourcePath = null;
                var iniFiles = Directory.GetFiles(tempExtractionDir, "*.ini", SearchOption.AllDirectories);
                foreach (var iniFile in iniFiles)
                {
                    string? parentDir = Path.GetFileName(Path.GetDirectoryName(iniFile))?.ToLowerInvariant();
                    string fileName = Path.GetFileName(iniFile).ToLowerInvariant();

                    if ((parentDir == "plugins" || parentDir == "plugin") && fileName.Contains("fusionfix"))
                    {
                        defaultIniSourcePath = iniFile;
                        break;
                    }
                }

                if (defaultIniSourcePath == null)
                {
                    defaultIniSourcePath = iniFiles.FirstOrDefault(f => Path.GetFileName(f).ToLowerInvariant().Contains("fusionfix"));
                }

                if (defaultIniSourcePath != null && File.Exists(defaultIniSourcePath))
                {
                    string defaultIniDestPath = Path.Combine(_baseDir, "FusionFixDefault.ini");
                    File.Copy(defaultIniSourcePath, defaultIniDestPath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to cache default FusionFix .ini: {ex.Message}");
            }

            // Copy all extracted files into GTAIV game directory
            StatusText = "Installing FusionFix files to game directory...";
            string gamePath = ActiveProfile.GamePath;
            var manifest = await LoadToolsManifestAsync();
            manifest.RemoveAll(f => string.Equals(f.SourceTool, "FusionFix", StringComparison.OrdinalIgnoreCase));

            var newInstalled = new System.Collections.Generic.List<InstalledToolFile>();
            CopyDirectoryWithToolManifest(tempExtractionDir, gamePath, gamePath, "FusionFix", newInstalled);
            manifest.AddRange(newInstalled);
            await SaveToolsManifestAsync(manifest);

            // Restore custom settings if a backup exists
            RestoreSettingsForTool("FusionFix");

            // Update tool versions dictionary in profile
            var toolVersions = new Dictionary<string, string>(ActiveProfile.ToolVersions.ToDictionary(k => k.Key, v => v.Value));
            toolVersions["FusionFix"] = release.TagName;

            // Resolve Ultimate ASI Loader's version dynamically from GitHub
            string asiVersionTag = "Win32-latest";
            try
            {
                var asiRelease = await _backendToolManager.GetLatestReleaseAsync("ThirteenAG", "Ultimate-ASI-Loader");
                if (asiRelease != null && !string.IsNullOrEmpty(asiRelease.TagName))
                {
                    asiVersionTag = asiRelease.TagName;
                }
            }
            catch
            {
                // Fallback to Win32-latest if offline or rate-limited
            }

            toolVersions["ASILoader"] = $"{asiVersionTag} (bundle with FusionFix)";
            toolVersions["DXVK"] = "v2.6.2 (bundle with FusionFix)";
            
            var updatedProfile = ActiveProfile with { InstalledToolVersions = toolVersions };
            _profileManager.SaveProfile(Path.Combine(_profilesDir, $"{updatedProfile.Id}.json"), updatedProfile);
            ActiveProfile = updatedProfile;

            // Apply configuration bug fixes (VRAM, aspect ratio)
            await ApplyPostInstallationPatchesAsync(gamePath, "FusionFix");

            // Clean up temporary extraction
            try
            {
                Directory.Delete(tempExtractionDir, recursive: true);
                if (File.Exists(cacheZip)) File.Delete(cacheZip);
                string legacyZip = Path.Combine(_baseDir, "Cache", "GTAIV.EFLC.FusionFixLegacyAddon.zip");
                if (File.Exists(legacyZip)) File.Delete(legacyZip);
            }
            catch { /* Ignore cleanup errors */ }

            StatusText = $"Successfully installed FusionFix {release.TagName}!";
            MessageBox.Show($"FusionFix {release.TagName} has been successfully installed!\nASI Loader and DXVK are ready.", "Installation Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText = $"FusionFix installation failed: {ex.Message}";
            MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            UpdateBackendStatus();
            UpdateConflictsAndWatchdog();
        }
    }

    private void CopyDirectoryWithToolManifest(string sourceDir, string targetDir, string gamePath, string toolName, System.Collections.Generic.List<InstalledToolFile> installedFiles)
    {
        Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            if (string.Equals(toolName, "ScriptHook", StringComparison.OrdinalIgnoreCase) && 
                string.Equals(Path.GetFileName(file), "dsound.dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
            
            // Compute SHA256 of the copied file
            string hash = "";
            try
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var stream = new FileStream(targetFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] hashBytes = sha256.ComputeHash(stream);
                hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            catch { }

            installedFiles.Add(new InstalledToolFile(toolName, targetFile, hash));
        }

        foreach (string directory in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(directory);
            string targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectoryWithToolManifest(directory, targetSubDir, gamePath, toolName, installedFiles);
        }
    }

    private async Task UninstallFusionFixAsync()
    {
        await UninstallToolGenericAsync("FusionFix", "FusionFix");
    }

    internal async Task InstallScriptHookAsync()
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath) || !Directory.Exists(ActiveProfile.GamePath))
        {
            MessageBox.Show("Please select a valid game directory first.", "Cannot Install", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        StatusText = "Installing ScriptHook files to game directory...";

        try
        {
            var manifest = await LoadToolsManifestAsync();
            // Remove previous ScriptHook files if they exist in manifest to avoid duplicates
            manifest.RemoveAll(f => string.Equals(f.SourceTool, "ScriptHook", StringComparison.OrdinalIgnoreCase));

            string sourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ScriptHook");
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"ScriptHook assets not found at {sourceDir}. Please ensure the application was built correctly with the Assets folder.");
            }

            // Remove dsound.dll if it exists in the game directory to avoid conflicts with FusionFix's ASI Loader
            string dsoundPath = Path.Combine(ActiveProfile.GamePath, "dsound.dll");
            if (File.Exists(dsoundPath))
            {
                try
                {
                    File.Delete(dsoundPath);
                }
                catch { }
            }

            var newInstalled = new System.Collections.Generic.List<InstalledToolFile>();
            CopyDirectoryWithToolManifest(sourceDir, ActiveProfile.GamePath, ActiveProfile.GamePath, "ScriptHook", newInstalled);
            
            manifest.AddRange(newInstalled);
            await SaveToolsManifestAsync(manifest);

            var toolVersions = ActiveProfile.InstalledToolVersions != null
                ? new System.Collections.Generic.Dictionary<string, string>(ActiveProfile.InstalledToolVersions)
                : new System.Collections.Generic.Dictionary<string, string>();
            toolVersions["ScriptHook"] = "Local Asset";
            var updatedProfile = ActiveProfile with { InstalledToolVersions = toolVersions };
            _profileManager.SaveProfile(Path.Combine(_profilesDir, $"{updatedProfile.Id}.json"), updatedProfile);
            ActiveProfile = updatedProfile;

            StatusText = "Successfully installed ScriptHook!";
            MessageBox.Show("ScriptHook has been successfully installed!", "Installation Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText = $"ScriptHook installation failed: {ex.Message}";
            MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            UpdateBackendStatus();
            UpdateConflictsAndWatchdog();
        }
    }

    private async Task UninstallScriptHookAsync()
    {
        await UninstallToolGenericAsync("ScriptHook", "ScriptHook");
    }

    private async Task InstallMemBiterAsync()
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath) || !Directory.Exists(ActiveProfile.GamePath))
        {
            MessageBox.Show("Please select a valid game directory first.", "Cannot Install", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        StatusText = "Installing MemBiter files to game directory...";

        try
        {
            var manifest = await LoadToolsManifestAsync();
            // Remove previous MemBiter files if they exist in manifest to avoid duplicates
            manifest.RemoveAll(f => string.Equals(f.SourceTool, "MemBiter", StringComparison.OrdinalIgnoreCase));

            string sourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "MinorDependencies", "MemBiter");
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"MemBiter assets not found at {sourceDir}. Please ensure the application was built correctly with the Assets folder.");
            }

            var newInstalled = new System.Collections.Generic.List<InstalledToolFile>();
            CopyDirectoryWithToolManifest(sourceDir, ActiveProfile.GamePath, ActiveProfile.GamePath, "MemBiter", newInstalled);
            
            manifest.AddRange(newInstalled);
            await SaveToolsManifestAsync(manifest);

            var toolVersions = ActiveProfile.InstalledToolVersions != null
                ? new System.Collections.Generic.Dictionary<string, string>(ActiveProfile.InstalledToolVersions)
                : new System.Collections.Generic.Dictionary<string, string>();
            toolVersions["MemBiter"] = "v1.1";
            var updatedProfile = ActiveProfile with { InstalledToolVersions = toolVersions };
            _profileManager.SaveProfile(Path.Combine(_profilesDir, $"{updatedProfile.Id}.json"), updatedProfile);
            ActiveProfile = updatedProfile;

            StatusText = "Successfully installed MemBiter!";
            MessageBox.Show("MemBiter has been successfully installed!", "Installation Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText = $"MemBiter installation failed: {ex.Message}";
            MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            UpdateBackendStatus();
            UpdateConflictsAndWatchdog();
        }
    }

    private async Task UninstallMemBiterAsync()
    {
        await UninstallToolGenericAsync("MemBiter", "MemBiter");
    }

    private async Task InstallBassAudioAsync()
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath) || !Directory.Exists(ActiveProfile.GamePath))
        {
            MessageBox.Show("Please select a valid game directory first.", "Cannot Install", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        StatusText = "Installing BASS Audio Library files to game directory...";

        try
        {
            var manifest = await LoadToolsManifestAsync();
            // Remove previous BassAudio files if they exist in manifest to avoid duplicates
            manifest.RemoveAll(f => string.Equals(f.SourceTool, "BassAudio", StringComparison.OrdinalIgnoreCase));

            string sourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "MinorDependencies", "BassAudio");
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"BassAudio assets not found at {sourceDir}. Please ensure the application was built correctly with the Assets folder.");
            }

            var newInstalled = new System.Collections.Generic.List<InstalledToolFile>();
            CopyDirectoryWithToolManifest(sourceDir, ActiveProfile.GamePath, ActiveProfile.GamePath, "BassAudio", newInstalled);
            
            manifest.AddRange(newInstalled);
            await SaveToolsManifestAsync(manifest);

            var toolVersions = ActiveProfile.InstalledToolVersions != null
                ? new System.Collections.Generic.Dictionary<string, string>(ActiveProfile.InstalledToolVersions)
                : new System.Collections.Generic.Dictionary<string, string>();
            toolVersions["BassAudio"] = "v2.4";
            var updatedProfile = ActiveProfile with { InstalledToolVersions = toolVersions };
            _profileManager.SaveProfile(Path.Combine(_profilesDir, $"{updatedProfile.Id}.json"), updatedProfile);
            ActiveProfile = updatedProfile;

            StatusText = "Successfully installed BASS Audio Library!";
            MessageBox.Show("BASS Audio Library has been successfully installed!", "Installation Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText = $"BASS Audio Library installation failed: {ex.Message}";
            MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            UpdateBackendStatus();
            UpdateConflictsAndWatchdog();
        }
    }

    private async Task UninstallBassAudioAsync()
    {
        await UninstallToolGenericAsync("BassAudio", "BassAudio");
    }

    private async Task<System.Collections.Generic.List<InstalledToolFile>> LoadToolsManifestAsync()
    {
        if (ActiveProfile == null) return new System.Collections.Generic.List<InstalledToolFile>();
        string manifestPath = Path.Combine(_profilesDir, $"{ActiveProfile.Id}_tools_manifest.json");
        if (!File.Exists(manifestPath)) return new System.Collections.Generic.List<InstalledToolFile>();

        try
        {
            string json = await File.ReadAllTextAsync(manifestPath);
            // Try new format
            try
            {
                var list = JsonSerializer.Deserialize<System.Collections.Generic.List<InstalledToolFile>>(json);
                if (list != null && list.Count > 0 && list[0].InstalledPath != null)
                {
                    return list;
                }
            }
            catch { }

            // Try old format
            var oldList = JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(json);
            if (oldList != null)
            {
                var migrated = new System.Collections.Generic.List<InstalledToolFile>();
                foreach (var relPath in oldList)
                {
                    string absPath = Path.Combine(ActiveProfile.GamePath, relPath);
                    string hash = "";
                    if (File.Exists(absPath))
                    {
                        hash = await _backendToolManager.ComputeSha256Async(absPath);
                    }
                    migrated.Add(new InstalledToolFile("FusionFix", absPath, hash));
                }
                return migrated;
            }
        }
        catch { }

        return new System.Collections.Generic.List<InstalledToolFile>();
    }

    private async Task SaveToolsManifestAsync(System.Collections.Generic.List<InstalledToolFile> manifest)
    {
        if (ActiveProfile == null) return;
        string manifestPath = Path.Combine(_profilesDir, $"{ActiveProfile.Id}_tools_manifest.json");
        string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json);
    }

    private async Task UninstallToolGenericAsync(string toolName, string displayName)
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath) || !Directory.Exists(ActiveProfile.GamePath))
        {
            MessageBox.Show("Please select a valid game directory first.", "Cannot Uninstall", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Are you sure you want to uninstall {displayName} from the game directory?",
            "Confirm Uninstall",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.No) return;

        IsBusy = true;
        StatusText = $"Uninstalling {displayName}...";

        try
        {
            var manifest = await LoadToolsManifestAsync();
            var toRemove = manifest.Where(f => string.Equals(f.SourceTool, toolName, StringComparison.OrdinalIgnoreCase)).ToList();

            // Always clean up generated config and log files if uninstalling DXVK, FusionFix, or ASILoader
            if (string.Equals(toolName, "DXVK", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(toolName, "FusionFix", StringComparison.OrdinalIgnoreCase))
            {
                string cfgPath = Path.Combine(ActiveProfile.GamePath, "d3d9.cfg");
                if (File.Exists(cfgPath))
                {
                    try { File.Delete(cfgPath); } catch { }
                }
                string logPath = Path.Combine(ActiveProfile.GamePath, "GTAIV_d3d9.log");
                if (File.Exists(logPath))
                {
                    try { File.Delete(logPath); } catch { }
                }
            }
            if (string.Equals(toolName, "FusionFix", StringComparison.OrdinalIgnoreCase))
            {
                string logPath = Path.Combine(ActiveProfile.GamePath, "FusionFix.log");
                if (File.Exists(logPath))
                {
                    try { File.Delete(logPath); } catch { }
                }
            }
            if (string.Equals(toolName, "ASILoader", StringComparison.OrdinalIgnoreCase))
            {
                string logPath = Path.Combine(ActiveProfile.GamePath, "dinput8.log");
                if (File.Exists(logPath))
                {
                    try { File.Delete(logPath); } catch { }
                }
            }

            if (toRemove.Count > 0)
            {
                // Delete files
                foreach (var file in toRemove)
                {
                    if (File.Exists(file.InstalledPath))
                    {
                        try { File.Delete(file.InstalledPath); } catch { }
                    }
                    manifest.Remove(file);
                }

                // Delete empty directories created (in reverse order of length)
                var dirsToCheck = toRemove
                    .Select(f => Path.GetDirectoryName(f.InstalledPath))
                    .Where(d => d != null && d != ActiveProfile.GamePath && d.StartsWith(ActiveProfile.GamePath))
                    .Distinct()
                    .OrderByDescending(d => d!.Length)
                    .ToList();

                foreach (var dir in dirsToCheck)
                {
                    if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        try { Directory.Delete(dir); } catch { }
                    }
                }

                await SaveToolsManifestAsync(manifest);
            }
            else
            {
                // Fallback for default known files of that tool if manifest didn't track it
                var defaultFiles = GetDefaultFilesForTool(toolName);
                foreach (var relPath in defaultFiles)
                {
                    string absPath = Path.Combine(ActiveProfile.GamePath, relPath);
                    if (File.Exists(absPath))
                    {
                        try { File.Delete(absPath); } catch { }
                    }
                }
            }

            // Remove manifest file if it is empty now
            string manifestPath = Path.Combine(_profilesDir, $"{ActiveProfile.Id}_tools_manifest.json");
            if (File.Exists(manifestPath))
            {
                var currentManifest = await LoadToolsManifestAsync();
                if (currentManifest.Count == 0)
                {
                    try { File.Delete(manifestPath); } catch { }
                }
            }

            // Update tool versions dictionary in profile
            var toolVersions = new Dictionary<string, string>(ActiveProfile.ToolVersions.ToDictionary(k => k.Key, v => v.Value));
            if (string.Equals(toolName, "FusionFix", StringComparison.OrdinalIgnoreCase))
            {
                toolVersions.Remove("FusionFix");
                if (toolVersions.TryGetValue("ASILoader", out string? vAsi) &&
                    (vAsi == "Bundled with FusionFix" ||
                     vAsi.Contains("bundled with FusionFix", StringComparison.OrdinalIgnoreCase) ||
                     vAsi.Contains("bundle with FusionFix", StringComparison.OrdinalIgnoreCase)))
                {
                    toolVersions.Remove("ASILoader");
                }
                if (toolVersions.TryGetValue("DXVK", out string? vDxvk) &&
                    (vDxvk == "Bundled with FusionFix" ||
                     vDxvk.Contains("bundled with FusionFix", StringComparison.OrdinalIgnoreCase) ||
                     vDxvk.Contains("bundle with FusionFix", StringComparison.OrdinalIgnoreCase)))
                {
                    toolVersions.Remove("DXVK");
                }
            }
            else
            {
                toolVersions.Remove(toolName);
            }
            
            var updatedProfile = ActiveProfile with { InstalledToolVersions = toolVersions };
            _profileManager.SaveProfile(Path.Combine(_profilesDir, $"{updatedProfile.Id}.json"), updatedProfile);
            ActiveProfile = updatedProfile;

            StatusText = $"{displayName} uninstalled successfully.";
            MessageBox.Show($"{displayName} has been uninstalled successfully.", "Uninstall Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText = $"Uninstallation failed: {ex.Message}";
            MessageBox.Show($"Uninstallation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            UpdateBackendStatus();
            UpdateConflictsAndWatchdog();
        }
    }

    private System.Collections.Generic.IEnumerable<string> GetDefaultFilesForTool(string toolName)
    {
        if (string.Equals(toolName, "FusionFix", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                "dinput8.dll", "d3d9.dll", "vulkan.dll", "d3d9.cfg",
                "plugins/GTAIV.EAFLC.FusionFix.asi", "plugins/GTAIV.EFLC.FusionFix.ini",
                "plugins/GTAIV.FusionFix.asi", "plugins/FusionFix.asi", "plugins/FusionFix.ini"
            };
        }
        else if (string.Equals(toolName, "ASILoader", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "dinput8.dll" };
        }
        else if (string.Equals(toolName, "DXVK", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "d3d9.dll", "vulkan.dll", "dxvk.conf", "commandline.txt", "d3d9.cfg" };
        }
        return System.Array.Empty<string>();
    }

    internal async Task InstallAsiLoaderAsync()
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath) || !Directory.Exists(ActiveProfile.GamePath))
        {
            MessageBox.Show("Please select a valid game directory first.", "Cannot Install", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Install guard: If FusionFix is already installed, skip standalone ASI Loader installation entirely
        var manifest = await LoadToolsManifestAsync();
        bool hasFusionFix = manifest.Any(f => string.Equals(f.SourceTool, "FusionFix", StringComparison.OrdinalIgnoreCase));
        if (hasFusionFix || BackendStatus.FusionFixInstalled)
        {
            MessageBox.Show("FusionFix is already installed and contains its own ASI Loader. Standalone ASI Loader installation is skipped.", "Installation Skipped", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsBusy = true;
        StatusText = "Querying latest Ultimate ASI Loader release...";

        try
        {
            var release = await _backendToolManager.GetLatestReleaseAsync("ThirteenAG", "Ultimate-ASI-Loader");
            if (release.Assets == null)
            {
                throw new Exception("No assets found in the latest Ultimate ASI Loader release.");
            }
            
            // Prefer Ultimate-ASI-Loader.zip (x86), fallback to Ultimate-ASI-Loader_x64.zip
            string assetName = release.Assets.ContainsKey("Ultimate-ASI-Loader.zip") 
                ? "Ultimate-ASI-Loader.zip" 
                : "Ultimate-ASI-Loader_x64.zip";

            if (!release.Assets.ContainsKey(assetName))
            {
                throw new Exception($"Could not find '{assetName}' asset in the latest Ultimate ASI Loader release.");
            }

            string downloadUrl = release.Assets[assetName];
            string cacheZip = Path.Combine(_baseDir, "Cache", assetName);

            StatusText = $"Downloading {assetName}...";
            await _backendToolManager.DownloadToolAsync(downloadUrl, cacheZip);

            string tempExtractionDir = Path.Combine(_baseDir, "Cache", "AsiLoaderTemp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempExtractionDir);

            StatusText = "Extracting ASI Loader...";
            await _archiveHandler.ExtractAsync(cacheZip, tempExtractionDir);

            string gamePath = ActiveProfile.GamePath;
            string sourceDll = Path.Combine(tempExtractionDir, "dinput8.dll");
            
            if (!File.Exists(sourceDll))
            {
                sourceDll = Directory.GetFiles(tempExtractionDir, "dinput8.dll", SearchOption.AllDirectories).FirstOrDefault() 
                    ?? throw new FileNotFoundException("dinput8.dll was not found in the extracted ASI Loader archive.");
            }

            string targetDll = Path.Combine(gamePath, "dinput8.dll");
            File.Copy(sourceDll, targetDll, overwrite: true);

            string hash = await _backendToolManager.ComputeSha256Async(targetDll);

            // Record to manifest
            manifest.RemoveAll(f => string.Equals(f.SourceTool, "ASILoader", StringComparison.OrdinalIgnoreCase));
            manifest.Add(new InstalledToolFile("ASILoader", targetDll, hash));
            await SaveToolsManifestAsync(manifest);

            // Update tool versions dictionary in profile
            var toolVersions = new Dictionary<string, string>(ActiveProfile.ToolVersions.ToDictionary(k => k.Key, v => v.Value));
            toolVersions["ASILoader"] = release.TagName;
            
            var updatedProfile = ActiveProfile with { InstalledToolVersions = toolVersions };
            _profileManager.SaveProfile(Path.Combine(_profilesDir, $"{updatedProfile.Id}.json"), updatedProfile);
            ActiveProfile = updatedProfile;

            // Clean up temp files
            try
            {
                Directory.Delete(tempExtractionDir, recursive: true);
                if (File.Exists(cacheZip)) File.Delete(cacheZip);
            }
            catch { }

            StatusText = $"Successfully installed Ultimate ASI Loader {release.TagName}!";
            MessageBox.Show($"Ultimate ASI Loader {release.TagName} has been successfully installed!", "Installation Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText = $"ASI Loader installation failed: {ex.Message}";
            MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            UpdateBackendStatus();
            UpdateConflictsAndWatchdog();
        }
    }

    private async Task UninstallAsiLoaderAsync()
    {
        await UninstallToolGenericAsync("ASILoader", "Ultimate ASI Loader");
    }

    private async Task InstallDxvkAsync()
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath) || !Directory.Exists(ActiveProfile.GamePath))
        {
            MessageBox.Show("Please select a valid game directory first.", "Cannot Install", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Install guard: If FusionFix is already installed, skip standalone DXVK installation entirely
        var manifest = await LoadToolsManifestAsync();
        bool hasFusionFix = manifest.Any(f => string.Equals(f.SourceTool, "FusionFix", StringComparison.OrdinalIgnoreCase));
        if (hasFusionFix || BackendStatus.FusionFixInstalled)
        {
            MessageBox.Show("FusionFix is already installed and contains its own DXVK layer. Standalone DXVK installation is skipped.", "Installation Skipped", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsBusy = true;
        StatusText = "Querying latest DXVK release from GitHub...";

        try
        {
            var release = await _backendToolManager.GetLatestReleaseAsync("doitsujin", "dxvk");
            if (release.Assets == null)
            {
                throw new Exception("No assets found in the latest DXVK release.");
            }
            
            bool isLegacy = ActiveProfile.LastKnownVersion != null && !ActiveProfile.LastKnownVersion.IsCompleteEdition;
            string? assetKey = null;

            if (isLegacy)
            {
                // Legacy GTA IV: use native variant containing "native" in filename
                assetKey = release.Assets.Keys.FirstOrDefault(k => k.Contains("native") && k.EndsWith(".tar.gz"));
                if (assetKey == null)
                {
                    assetKey = release.Assets.Keys.FirstOrDefault(k => k.EndsWith(".tar.gz") && !k.Contains("debug"));
                }
            }
            else
            {
                // CE version: use standard release asset (.tar.gz, no native, no debug)
                assetKey = release.Assets.Keys.FirstOrDefault(k => k.EndsWith(".tar.gz") && !k.Contains("native") && !k.Contains("debug"));
            }

            if (string.IsNullOrEmpty(assetKey))
            {
                throw new Exception("Could not find a suitable DXVK .tar.gz release asset.");
            }

            string downloadUrl = release.Assets[assetKey];
            string cacheTarGz = Path.Combine(_baseDir, "Cache", assetKey);

            StatusText = $"Downloading {assetKey}...";
            await _backendToolManager.DownloadToolAsync(downloadUrl, cacheTarGz);

            string tempExtractionDir = Path.Combine(_baseDir, "Cache", "DxvkTemp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempExtractionDir);

            StatusText = "Extracting DXVK tar.gz...";
            await _archiveHandler.ExtractAsync(cacheTarGz, tempExtractionDir);

            // Locate d3d9.dll in x32 folder (ignore x64)
            string[] matchingFiles = Directory.GetFiles(tempExtractionDir, "d3d9.dll", SearchOption.AllDirectories);
            string? d3d9Source = matchingFiles.FirstOrDefault(f => f.Split(Path.DirectorySeparatorChar).Contains("x32"));

            if (string.IsNullOrEmpty(d3d9Source) || !File.Exists(d3d9Source))
            {
                throw new FileNotFoundException("d3d9.dll was not found in the x32 folder of the extracted DXVK archive.");
            }

            string gamePath = ActiveProfile.GamePath;
            
            // If FusionFix is present, rename to vulkan.dll. Otherwise d3d9.dll
            string targetFileName = (hasFusionFix || BackendStatus.FusionFixInstalled) ? "vulkan.dll" : "d3d9.dll";
            string targetPath = Path.Combine(gamePath, targetFileName);

            File.Copy(d3d9Source, targetPath, overwrite: true);

            string hash = await _backendToolManager.ComputeSha256Async(targetPath);

            // Record to manifest
            manifest.RemoveAll(f => string.Equals(f.SourceTool, "DXVK", StringComparison.OrdinalIgnoreCase));
            manifest.Add(new InstalledToolFile("DXVK", targetPath, hash));
            await SaveToolsManifestAsync(manifest);

            // Restore custom settings if a backup exists
            RestoreSettingsForTool("DXVK");

            // Update tool versions dictionary in profile
            var toolVersions = new Dictionary<string, string>(ActiveProfile.ToolVersions.ToDictionary(k => k.Key, v => v.Value));
            toolVersions["DXVK"] = release.TagName;
            
            var updatedProfile = ActiveProfile with { InstalledToolVersions = toolVersions };
            _profileManager.SaveProfile(Path.Combine(_profilesDir, $"{updatedProfile.Id}.json"), updatedProfile);
            ActiveProfile = updatedProfile;

            // Apply configuration bug fixes
            await ApplyPostInstallationPatchesAsync(gamePath, "DXVK");

            // Clean up
            try
            {
                Directory.Delete(tempExtractionDir, recursive: true);
                if (File.Exists(cacheTarGz)) File.Delete(cacheTarGz);
            }
            catch { }

            StatusText = $"Successfully installed DXVK {release.TagName}!";
            MessageBox.Show($"DXVK {release.TagName} has been successfully installed!", "Installation Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText = $"DXVK installation failed: {ex.Message}";
            MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            UpdateBackendStatus();
            UpdateConflictsAndWatchdog();
        }
    }

    private async Task UninstallDxvkAsync()
    {
        await UninstallToolGenericAsync("DXVK", "DXVK");
    }

    private async Task ApplyPostInstallationPatchesAsync(string gamePath, string sourceTool)
    {
        // Fix 1: VRAM Detection Bug
        int vramVal = GpuVramMb;
        string commandlinePath = Path.Combine(gamePath, "commandline.txt");
        bool commandlineCreated = false;
        if (!File.Exists(commandlinePath))
        {
            commandlineCreated = true;
        }
        UpdateCommandLineTxt(gamePath, vramVal);
        
        // Record commandline.txt in manifest if created
        if (commandlineCreated)
        {
            var manifest = await LoadToolsManifestAsync();
            if (!manifest.Any(f => f.InstalledPath.Equals(commandlinePath, StringComparison.OrdinalIgnoreCase)))
            {
                string hash = await _backendToolManager.ComputeSha256Async(commandlinePath);
                manifest.Add(new InstalledToolFile(sourceTool, commandlinePath, hash));
                await SaveToolsManifestAsync(manifest);
            }
        }

        // Fix 2: Resolution Not Scaling Bug
        string dxvkConfPath = Path.Combine(gamePath, "dxvk.conf");
        bool dxvkConfCreated = false;
        if (!File.Exists(dxvkConfPath))
        {
            dxvkConfCreated = true;
            try
            {
                string dxvkConfUrl = "https://raw.githubusercontent.com/doitsujin/dxvk/master/dxvk.conf";
                await _backendToolManager.DownloadToolAsync(dxvkConfUrl, dxvkConfPath);
            }
            catch
            {
                // Fallback: write a basic forceAspectRatio entry if download fails
                await File.WriteAllTextAsync(dxvkConfPath, "# d3d9.forceAspectRatio = \"\"");
            }
        }

        if (File.Exists(dxvkConfPath))
        {
            try
            {
                double width = SystemParameters.PrimaryScreenWidth;
                double height = SystemParameters.PrimaryScreenHeight;
                double ratio = width / height;
                string aspectVal = ratio > 2.0 ? "21:9" : "16:9";

                string content = await File.ReadAllTextAsync(dxvkConfPath);
                if (content.Contains("# d3d9.forceAspectRatio = \"\""))
                {
                    content = content.Replace("# d3d9.forceAspectRatio = \"\"", $"d3d9.forceAspectRatio = \"{aspectVal}\"");
                }
                else if (content.Contains("#d3d9.forceAspectRatio = \"\""))
                {
                    content = content.Replace("#d3d9.forceAspectRatio = \"\"", $"d3d9.forceAspectRatio = \"{aspectVal}\"");
                }
                else if (!content.Contains("d3d9.forceAspectRatio"))
                {
                    content += $"\r\nd3d9.forceAspectRatio = \"{aspectVal}\"\r\n";
                }
                await File.WriteAllTextAsync(dxvkConfPath, content);

                if (dxvkConfCreated)
                {
                    var manifest = await LoadToolsManifestAsync();
                    if (!manifest.Any(f => f.InstalledPath.Equals(dxvkConfPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        string hash = await _backendToolManager.ComputeSha256Async(dxvkConfPath);
                        manifest.Add(new InstalledToolFile(sourceTool, dxvkConfPath, hash));
                        await SaveToolsManifestAsync(manifest);
                    }
                }
            }
            catch { }
        }
    }

    private void UpdateCommandLineTxt(string gamePath, int vramMb)
    {
        string filePath = Path.Combine(gamePath, "commandline.txt");
        System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
        if (File.Exists(filePath))
        {
            try
            {
                lines = File.ReadAllLines(filePath).ToList();
            }
            catch { }
        }

        // Remove existing keys if any
        lines.RemoveAll(l => l.StartsWith("-availablevidmem", StringComparison.OrdinalIgnoreCase));
        lines.RemoveAll(l => l.StartsWith("-nomemrestrict", StringComparison.OrdinalIgnoreCase));
        lines.RemoveAll(l => l.StartsWith("-norestrictions", StringComparison.OrdinalIgnoreCase));

        // Append new ones
        lines.Add($"-availablevidmem {vramMb}");
        lines.Add("-nomemrestrict");
        lines.Add("-norestrictions");

        try
        {
            File.WriteAllLines(filePath, lines);
        }
        catch { }
    }

    private void BackupSettings()
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath)) return;

        try
        {
            string backupDir = Path.Combine(_profilesDir, "backups", ActiveProfile.Id);
            Directory.CreateDirectory(backupDir);

            // Backup FusionFix settings
            string fusionFixIni = Path.Combine(ActiveProfile.GamePath, "plugins", "GTAIV.EFLC.FusionFix.ini");
            if (File.Exists(fusionFixIni))
            {
                File.Copy(fusionFixIni, Path.Combine(backupDir, "GTAIV.EFLC.FusionFix.ini.bak"), overwrite: true);
            }

            // Backup DXVK settings
            string dxvkConf = Path.Combine(ActiveProfile.GamePath, "dxvk.conf");
            if (File.Exists(dxvkConf))
            {
                File.Copy(dxvkConf, Path.Combine(backupDir, "dxvk.conf.bak"), overwrite: true);
            }

            // Backup commandline settings
            string commandline = Path.Combine(ActiveProfile.GamePath, "commandline.txt");
            if (File.Exists(commandline))
            {
                File.Copy(commandline, Path.Combine(backupDir, "commandline.txt.bak"), overwrite: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to backup settings: {ex.Message}");
        }
    }

    private void RestoreSettingsForTool(string toolName)
    {
        if (ActiveProfile == null || string.IsNullOrEmpty(ActiveProfile.GamePath)) return;

        try
        {
            string backupDir = Path.Combine(_profilesDir, "backups", ActiveProfile.Id);
            if (!Directory.Exists(backupDir)) return;

            if (string.Equals(toolName, "FusionFix", StringComparison.OrdinalIgnoreCase))
            {
                string backupFile = Path.Combine(backupDir, "GTAIV.EFLC.FusionFix.ini.bak");
                string targetFile = Path.Combine(ActiveProfile.GamePath, "plugins", "GTAIV.EFLC.FusionFix.ini");
                if (File.Exists(backupFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                    File.Copy(backupFile, targetFile, overwrite: true);
                }
            }
            else if (string.Equals(toolName, "DXVK", StringComparison.OrdinalIgnoreCase))
            {
                string backupConf = Path.Combine(backupDir, "dxvk.conf.bak");
                string targetConf = Path.Combine(ActiveProfile.GamePath, "dxvk.conf");
                if (File.Exists(backupConf))
                {
                    File.Copy(backupConf, targetConf, overwrite: true);
                }

                string backupCmd = Path.Combine(backupDir, "commandline.txt.bak");
                string targetCmd = Path.Combine(ActiveProfile.GamePath, "commandline.txt");
                if (File.Exists(backupCmd))
                {
                    File.Copy(backupCmd, targetCmd, overwrite: true);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore settings for {toolName}: {ex.Message}");
        }
    }
}

public class AppSettings
{
    public bool IsDarkTheme { get; set; } = true;
    public string GtaSaveProfilesPath { get; set; } = "";
}

public record ActiveMergedUpdateFileViewModel(string ModName, string RelativePath)
{
    public string Root => RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
}
