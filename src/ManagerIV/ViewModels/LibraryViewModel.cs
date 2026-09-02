using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using ManagerIV.Core;

namespace ManagerIV.ViewModels;

public enum ModCategoryFilter
{
    All,
    Assets,
    Plugins,
    Scripts
}

public class LibraryViewModel: ViewModelBase
{
    public MainViewModel MainVM { get; set; } = null!;
    private ObservableCollection<ModViewModel> _libraryMods = new();
    private ModViewModel? _selectedLibraryMod;
    private ModViewModel? _selectedPluginMod;
    private ModViewModel? _selectedScriptMod;
    private ModCategoryFilter _selectedCategoryFilter = ModCategoryFilter.Assets;

    private bool _isEditingMod;
    private ModViewModel? _editingMod;
    private string _editingName = "";
    private string _editingVersion = "";
    private string _editingCompatibility = "";
    private string _editingDescription = "";

    public bool IsEditingMod
    {
        get => _isEditingMod;
        set => SetProperty(ref _isEditingMod, value);
    }

    public ModViewModel? EditingMod
    {
        get => _editingMod;
        set => SetProperty(ref _editingMod, value);
    }

    public string EditingName
    {
        get => _editingName;
        set => SetProperty(ref _editingName, value);
    }

    public string EditingVersion
    {
        get => _editingVersion;
        set => SetProperty(ref _editingVersion, value);
    }

    public string EditingCompatibility
    {
        get => _editingCompatibility;
        set => SetProperty(ref _editingCompatibility, value);
    }

    public string EditingDescription
    {
        get => _editingDescription;
        set => SetProperty(ref _editingDescription, value);
    }

    public ObservableCollection<ModViewModel> LibraryMods
    {
        get => _libraryMods;
        set => SetProperty(ref _libraryMods, value);
    }

    public System.ComponentModel.ICollectionView? MainModsCollection { get; }
    public System.ComponentModel.ICollectionView? PluginsCollection { get; }
    public System.ComponentModel.ICollectionView? ScriptsCollection { get; }
    public System.ComponentModel.ICollectionView? UnifiedModsCollection { get; }

    public ModCategoryFilter SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                OnPropertyChanged(nameof(IsAllFilterActive));
                OnPropertyChanged(nameof(IsAssetsFilterActive));
                OnPropertyChanged(nameof(IsPluginsFilterActive));
                OnPropertyChanged(nameof(IsScriptsFilterActive));
                ApplyFilter();
            }
        }
    }

    public bool IsAllFilterActive
    {
        get => SelectedCategoryFilter == ModCategoryFilter.All;
        set { if (value) SelectedCategoryFilter = ModCategoryFilter.All; }
    }

    public bool IsAssetsFilterActive
    {
        get => SelectedCategoryFilter == ModCategoryFilter.Assets;
        set { if (value) SelectedCategoryFilter = ModCategoryFilter.Assets; }
    }

    public bool IsPluginsFilterActive
    {
        get => SelectedCategoryFilter == ModCategoryFilter.Plugins;
        set { if (value) SelectedCategoryFilter = ModCategoryFilter.Plugins; }
    }

    public bool IsScriptsFilterActive
    {
        get => SelectedCategoryFilter == ModCategoryFilter.Scripts;
        set { if (value) SelectedCategoryFilter = ModCategoryFilter.Scripts; }
    }

    public int AllModsCount => LibraryMods.Count;
    public int AssetsCount => LibraryMods.Count(m => m.Target == DeployTarget.Update);
    public int PluginsCount => LibraryMods.Count(m => m.Target == DeployTarget.Plugins);
    public int ScriptsCount => LibraryMods.Count(m => m.Target == DeployTarget.Scripts);

    public void NotifyCountsChanged()
    {
        OnPropertyChanged(nameof(AllModsCount));
        OnPropertyChanged(nameof(AssetsCount));
        OnPropertyChanged(nameof(PluginsCount));
        OnPropertyChanged(nameof(ScriptsCount));
    }

    public LibraryViewModel()
    {
        if (System.Windows.Application.Current != null)
        {
            MainModsCollection = new System.Windows.Data.ListCollectionView(LibraryMods);
            PluginsCollection = new System.Windows.Data.ListCollectionView(LibraryMods);
            ScriptsCollection = new System.Windows.Data.ListCollectionView(LibraryMods);
            UnifiedModsCollection = new System.Windows.Data.ListCollectionView(LibraryMods);
        }

        _libraryMods.CollectionChanged += (s, e) =>
        {
            NotifyCountsChanged();
        };

        ToggleModEnabledCommand = new RelayCommand<ModViewModel>(ToggleModEnabled);
        ReorderModCommand = new RelayCommand<Tuple<ModViewModel, int>?>(ReorderMod);
        OpenEditModCommand = new RelayCommand<ModViewModel>(OpenEditMod);
        CloseEditModCommand = new RelayCommand(CloseEditMod);
        SaveModDetailsCommand = new RelayCommand(SaveModDetails);
        ClearLibraryCommand = new RelayCommand(async () => await ClearLibraryAsync());
        OpenLibraryDirCommand = new RelayCommand(OpenLibraryDirInExplorer);
        SelectCategoryCommand = new RelayCommand<string>(SelectCategory);
        
        ImportModArchiveCommand = new RelayCommand(async () =>
        {
            if (MainVM != null)
            {
                await MainVM.PromptAndImportArchiveAsync();
            }
        });

        DeleteModCommand = new RelayCommand<ModViewModel>(async mod =>
        {
            if (MainVM != null && mod != null)
            {
                await MainVM.DeleteModAsync(mod);
            }
        });
    }

    public ModViewModel? SelectedLibraryMod
    {
        get => _selectedLibraryMod;
        set
        {
            if (SetProperty(ref _selectedLibraryMod, value))
            {
                if (value != null)
                {
                    SelectedPluginMod = null;
                    SelectedScriptMod = null;
                    MainVM.SelectedMod = value;
                }
                else if (SelectedPluginMod == null && SelectedScriptMod == null)
                {
                    MainVM.SelectedMod = null;
                }
            }
        }
    }

    public ModViewModel? SelectedPluginMod
    {
        get => _selectedPluginMod;
        set
        {
            if (SetProperty(ref _selectedPluginMod, value))
            {
                if (value != null)
                {
                    SelectedLibraryMod = null;
                    SelectedScriptMod = null;
                    MainVM.SelectedMod = value;
                }
                else if (SelectedLibraryMod == null && SelectedScriptMod == null)
                {
                    MainVM.SelectedMod = null;
                }
            }
        }
    }

    public ModViewModel? SelectedScriptMod
    {
        get => _selectedScriptMod;
        set
        {
            if (SetProperty(ref _selectedScriptMod, value))
            {
                if (value != null)
                {
                    SelectedLibraryMod = null;
                    SelectedPluginMod = null;
                    MainVM.SelectedMod = value;
                }
                else if (SelectedLibraryMod == null && SelectedPluginMod == null)
                {
                    MainVM.SelectedMod = null;
                }
            }
        }
    }

    internal void ApplyFilter()
    {
        if (Application.Current == null || Application.Current.Dispatcher == null)
        {
            return;
        }

        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(ApplyFilter));
            return;
        }

        MainModsCollection?.Refresh();
        PluginsCollection?.Refresh();
        ScriptsCollection?.Refresh();
        UnifiedModsCollection?.Refresh();
    }
    public ICommand ToggleModEnabledCommand { get; }
    public ICommand ImportModArchiveCommand { get; }
    public ICommand ReorderModCommand { get; }
    public ICommand SaveModDetailsCommand { get; }
    public ICommand DeleteModCommand { get; }
    public ICommand ClearLibraryCommand { get; }
    public ICommand OpenLibraryDirCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand OpenEditModCommand { get; }
    public ICommand CloseEditModCommand { get; }

    internal void LoadLibrary()
    {
        LibraryMods.Clear();
        if (File.Exists(MainVM._libraryManifestFile))
        {
            try
            {
                string json = File.ReadAllText(MainVM._libraryManifestFile);
                var modsList = JsonSerializer.Deserialize<System.Collections.Generic.List<StagedMod>>(json);
                if (modsList != null)
                {
                    foreach (var mod in modsList)
                    {
                        var target = MainVM._loadOrderService.DetermineDeployTarget(mod);
                        LibraryMods.Add(new ModViewModel(ApplyDerivedLibraryTags(mod), false, 99, target));
                    }
                }
            }
            catch (Exception ex)
            {
                MainVM.StatusText = $"Failed to load mod library manifest: {ex.Message}";
            }
        }
    }

    internal void SaveLibrary()
    {
        try
        {
            var rawModels = LibraryMods.Select(m => m.Model).ToList();
            string json = JsonSerializer.Serialize(rawModels, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(MainVM._libraryManifestFile, json);
        }
        catch (Exception ex)
        {
            MainVM.StatusText = $"Failed to save library manifest: {ex.Message}";
        }
    }

    private void OpenLibraryDirInExplorer()
    {
        if (MainVM.ActiveProfile != null && !string.IsNullOrWhiteSpace(MainVM.ActiveProfile.LibraryPath) && Directory.Exists(MainVM.ActiveProfile.LibraryPath))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = MainVM.ActiveProfile.LibraryPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MainVM.StatusText = $"Failed to open folder: {ex.Message}";
            }
        }
    }

    internal static StagedMod ApplyDerivedLibraryTags(StagedMod mod)
    {
        var tags = (mod.Tags ?? Array.Empty<string>()).ToList();

        if (UpdateDeploymentClassifier.IsMergeOnlyUpdateMod(mod) &&
            !tags.Contains("invisible", StringComparer.OrdinalIgnoreCase))
        {
            tags.Add("invisible");
        }

        return mod with { Tags = tags };
    }

    private void SelectCategory(string? categoryName)
    {
        if (!string.IsNullOrWhiteSpace(categoryName) && Enum.TryParse<ModCategoryFilter>(categoryName, true, out var filter))
        {
            SelectedCategoryFilter = filter;
        }
    }

    private void OpenEditMod(ModViewModel? mod)
    {
        if (mod == null) return;
        EditingMod = mod;
        EditingName = mod.Name;
        EditingVersion = mod.Version;
        EditingCompatibility = mod.Compatibility;
        EditingDescription = mod.Description;
        IsEditingMod = true;
    }

    private void CloseEditMod()
    {
        IsEditingMod = false;
        EditingMod = null;
    }

    private async void ToggleModEnabled(ModViewModel? modVm)
    {
        if (modVm == null || MainVM.ActiveProfile == null) return;

        try
        {
            if (!modVm.IsEnabled)
            {
                if (modVm.Target == DeployTarget.Plugins)
                {
                    if (!MainVM.BackendStatus.AsiLoaderInstalled && !MainVM.BackendStatus.FusionFixInstalled)
                    {
                        var result = System.Windows.MessageBox.Show(
                            $"The mod '{modVm.Name}' is an ASI plugin and requires an ASI Loader to run, but it is not currently installed.\n\n" +
                            "Would you like to install the Ultimate ASI Loader now?",
                            "Missing ASI Loader",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning);

                        if (result == System.Windows.MessageBoxResult.Yes)
                        {
                            await MainVM.InstallAsiLoaderAsync();
                            if (!MainVM.BackendStatus.AsiLoaderInstalled && !MainVM.BackendStatus.FusionFixInstalled)
                            {
                                System.Windows.MessageBox.Show("Ultimate ASI Loader installation was not completed. Mod cannot be enabled.", "Missing Backend", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }
                else if (modVm.Target == DeployTarget.Scripts)
                {
                    if (!MainVM.BackendStatus.ScriptHookInstalled)
                    {
                        var result = System.Windows.MessageBox.Show(
                            $"The mod '{modVm.Name}' contains scripts and requires ScriptHook to run, but it is not currently installed.\n\n" +
                            "Would you like to install ScriptHook now?",
                            "Missing ScriptHook",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning);

                        if (result == System.Windows.MessageBoxResult.Yes)
                        {
                            await MainVM.InstallScriptHookAsync();
                            if (!MainVM.BackendStatus.ScriptHookInstalled)
                            {
                                System.Windows.MessageBox.Show("ScriptHook installation was not completed. Mod cannot be enabled.", "Missing Backend", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }

            modVm.IsEnabled = !modVm.IsEnabled;
            
            var list = MainVM.ActiveProfile.EnabledModIds.ToList();
            if (modVm.IsEnabled)
            {
                if (!list.Contains(modVm.Id)) list.Add(modVm.Id);
            }
            else
            {
                list.Remove(modVm.Id);
            }

            var updatedProfile = MainVM.ActiveProfile with { EnabledModIds = list };
            MainVM.SaveProfileState(updatedProfile);
            MainVM.RefreshActiveModsList();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to toggle mod enablement: {ex.Message}", "Mod Toggle Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void ReorderMod(Tuple<ModViewModel, int>? param)
    {
        if (param == null || MainVM.ActiveProfile == null) return;

        var modVm = param.Item1;
        int targetPriority = param.Item2;

        var newOrder = MainVM._loadOrderService.ReorderMod(MainVM.ActiveProfile.LoadOrder, modVm.Id, targetPriority);
        
        var updatedProfile = MainVM.ActiveProfile with { LoadOrder = newOrder };
        MainVM.SaveProfileState(updatedProfile);
        MainVM.RefreshActiveModsList();
    }

    private void SaveModDetails()
    {
        if (EditingMod != null)
        {
            EditingMod.Name = EditingName.Trim();
            EditingMod.Version = EditingVersion.Trim();
            EditingMod.Compatibility = EditingCompatibility.Trim();
            EditingMod.Description = EditingDescription.Trim();
            SaveLibrary();
            MainVM.StatusText = $"Saved details for '{EditingMod.Name}'.";
        }
        IsEditingMod = false;
        EditingMod = null;
    }

    private async Task ClearLibraryAsync()
    {
        if (!LibraryMods.Any()) return;

        var result = MessageBox.Show(
            "Are you sure you want to permanently delete ALL mods from the library?\n\nThis will physically undeploy all active mods from the game directory, remove them from all profiles, and delete all mod files from your disk. This action CANNOT be undone.",
            "Clear Mod Library",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.No)
        {
            return;
        }

        await ClearLibraryInternalAsync(showSuccessMessage: true);
    }

    public async Task ClearLibraryInternalAsync(bool showSuccessMessage = false)
    {
        MainVM.IsBusy = true;
        MainVM.StatusText = "Clearing mod library...";

        try
        {
            // 1. Undeploy all mods first from the current active game path
            if (MainVM.ActiveProfile != null && !string.IsNullOrEmpty(MainVM.ActiveProfile.GamePath) && Directory.Exists(MainVM.ActiveProfile.GamePath))
            {
                var adapter = new CompleteEditionAdapter(MainVM.ActiveProfile.GamePath, MainVM._linker);
                foreach (var modVm in LibraryMods)
                {
                    try
                    {
                        await adapter.UndeployAsync(modVm.Model);
                    }
                    catch { /* Ignore individual undeploy errors */ }
                }
            }

            // 2. Remove all mods from all profiles
            var profilesList = MainVM.Profiles.ToList();
            foreach (var profile in profilesList)
            {
                var updatedProfile = profile with
                {
                    EnabledModIds = Array.Empty<string>(),
                    LoadOrder = new LoadOrderModel(Array.Empty<LoadOrderEntry>())
                };
                MainVM.SaveProfileState(updatedProfile);
            }

            // 3. Delete physical library directory contents
            if (Directory.Exists(MainVM._libraryDir))
            {
                await Task.Run(() =>
                {
                    foreach (var dir in Directory.GetDirectories(MainVM._libraryDir))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                    foreach (var file in Directory.GetFiles(MainVM._libraryDir))
                    {
                        if (!Path.GetFileName(file).Equals("mods.json", StringComparison.OrdinalIgnoreCase))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                });
            }

            // 4. Clear library mods list and save manifest
            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    LibraryMods.Clear();
                });
            }
            else
            {
                LibraryMods.Clear();
            }
            SaveLibrary();

            // 5. Refresh active profile links list
            MainVM.RefreshActiveModsList();

            MainVM.StatusText = "Mod library cleared successfully.";
            if (showSuccessMessage)
            {
                MessageBox.Show("Mod library has been cleared completely.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MainVM.StatusText = $"Failed to clear mod library: {ex.Message}";
            if (showSuccessMessage)
            {
                MessageBox.Show($"Failed to clear library: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                throw;
            }
        }
        finally
        {
            MainVM.IsBusy = false;
            MainVM.UpdateConflictsAndWatchdog();
        }
    }
}


