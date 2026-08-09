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

public class LibraryViewModel: ViewModelBase
{
    private readonly string _libraryDir;
    private readonly string _libraryManifestFile;
    private ObservableCollection<ModViewModel> _libraryMods = new();
    private ModViewModel? _selectedLibraryMod;
    private ModViewModel? _selectedPluginMod;
    private ModViewModel? _selectedScriptMod;

    public ObservableCollection<ModViewModel> LibraryMods
    {
        get => _libraryMods;
        set => SetProperty(ref _libraryMods, value);
    }

    public System.ComponentModel.ICollectionView? MainModsCollection { get; }
    public System.ComponentModel.ICollectionView? PluginsCollection { get; }
    public System.ComponentModel.ICollectionView? ScriptsCollection { get; }

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
                    SelectedMod = value;
                }
                else if (SelectedPluginMod == null && SelectedScriptMod == null)
                {
                    SelectedMod = null;
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
                    SelectedMod = value;
                }
                else if (SelectedLibraryMod == null && SelectedScriptMod == null)
                {
                    SelectedMod = null;
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
                    SelectedMod = value;
                }
                else if (SelectedLibraryMod == null && SelectedPluginMod == null)
                {
                    SelectedMod = null;
                }
            }
        }
    }

    private void ApplyFilter()
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
    }
    public ICommand ToggleModEnabledCommand { get; }
    public ICommand ImportModArchiveCommand { get; }
    public ICommand ReorderModCommand { get; }
    public ICommand SaveModDetailsCommand { get; }
    public ICommand DeleteModCommand { get; }
    public ICommand ClearLibraryCommand { get; }
    public ICommand OpenLibraryDirCommand { get; }

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
                        var target = _loadOrderService.DetermineDeployTarget(mod);
                        LibraryMods.Add(new ModViewModel(ApplyDerivedLibraryTags(mod), false, 99, target));
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

    private void OpenLibraryDirInExplorer()
    {
        if (ActiveProfile != null && !string.IsNullOrWhiteSpace(ActiveProfile.LibraryPath) && Directory.Exists(ActiveProfile.LibraryPath))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ActiveProfile.LibraryPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to open folder: {ex.Message}";
            }
        }
    }

    private static StagedMod ApplyDerivedLibraryTags(StagedMod mod)
    {
        var tags = (mod.Tags ?? Array.Empty<string>()).ToList();

        if (UpdateDeploymentClassifier.IsMergeOnlyUpdateMod(mod) &&
            !tags.Contains("invisible", StringComparer.OrdinalIgnoreCase))
        {
            tags.Add("invisible");
        }

        return mod with { Tags = tags };
    }

    private async void ToggleModEnabled(ModViewModel? modVm)
    {
        if (modVm == null || ActiveProfile == null) return;

        if (!modVm.IsEnabled)
        {
            if (modVm.Target == DeployTarget.Plugins)
            {
                if (!BackendStatus.AsiLoaderInstalled && !BackendStatus.FusionFixInstalled)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"The mod '{modVm.Name}' is an ASI plugin and requires an ASI Loader to run, but it is not currently installed.\n\n" +
                        "Would you like to install the Ultimate ASI Loader now?",
                        "Missing ASI Loader",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        await InstallAsiLoaderAsync();
                        if (!BackendStatus.AsiLoaderInstalled && !BackendStatus.FusionFixInstalled)
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
                if (!BackendStatus.ScriptHookInstalled)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"The mod '{modVm.Name}' contains scripts and requires ScriptHook to run, but it is not currently installed.\n\n" +
                        "Would you like to install ScriptHook now?",
                        "Missing ScriptHook",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        await InstallScriptHookAsync();
                        if (!BackendStatus.ScriptHookInstalled)
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
        
        var list = ActiveProfile.EnabledModIds.ToList();
        if (modVm.IsEnabled)
        {
            if (!list.Contains(modVm.Id)) list.Add(modVm.Id);
        }
        else
        {
            list.Remove(modVm.Id);
        }

        var updatedProfile = ActiveProfile with { EnabledModIds = list };
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

    private void SaveModDetails()
    {
        SaveLibrary();
        StatusText = "Mod details saved successfully.";
        MessageBox.Show("Mod details saved successfully to the library manifest.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
        IsBusy = true;
        StatusText = "Clearing mod library...";

        try
        {
            // 1. Undeploy all mods first from the current active game path
            if (ActiveProfile != null && !string.IsNullOrEmpty(ActiveProfile.GamePath) && Directory.Exists(ActiveProfile.GamePath))
            {
                var adapter = new CompleteEditionAdapter(ActiveProfile.GamePath, _linker);
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
            var profilesList = Profiles.ToList();
            foreach (var profile in profilesList)
            {
                var updatedProfile = profile with
                {
                    EnabledModIds = Array.Empty<string>(),
                    LoadOrder = new LoadOrderModel(Array.Empty<LoadOrderEntry>())
                };
                SaveProfileState(updatedProfile);
            }

            // 3. Delete physical library directory contents
            if (Directory.Exists(_libraryDir))
            {
                await Task.Run(() =>
                {
                    foreach (var dir in Directory.GetDirectories(_libraryDir))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                    foreach (var file in Directory.GetFiles(_libraryDir))
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
            RefreshActiveModsList();

            StatusText = "Mod library cleared successfully.";
            if (showSuccessMessage)
            {
                MessageBox.Show("Mod library has been cleared completely.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to clear mod library: {ex.Message}";
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
            IsBusy = false;
            UpdateConflictsAndWatchdog();
        }
    }
}


