using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ManagerIV.Core;
using Microsoft.Extensions.Logging;

namespace ManagerIV.ViewModels;

public class SaveProfileViewModel : ViewModelBase
{
    private readonly ILogger<SaveProfileViewModel> _logger;
    private SaveProfileManager _saveProfileManager;
    private string _gtaSaveProfilesPath = "";
    private ObservableCollection<string> _baseProfileIds = new();
    private string? _selectedBaseProfileId;
    private ObservableCollection<SaveProfile> _saveProfiles = new();
    private SaveProfile? _selectedSaveProfile;
    private string _newSaveProfileName = "";
    private string _quickSnapshotName = "";
    private string _renameActiveSaveTo = "";

    public SaveProfileViewModel(ILogger<SaveProfileViewModel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        BrowseSaveProfilesPathCommand = new RelayCommand(BrowseSaveProfilesPath);
        ActivateSaveProfileCommand = new RelayCommand<object>(ActivateSaveProfile);
        CreateSaveProfileCommand = new RelayCommand(CreateSaveProfile);
        RenameSaveProfileCommand = new RelayCommand<string>(RenameSaveProfile);
        DeleteSaveProfileCommand = new RelayCommand(DeleteSaveProfile);
        RefreshSaveProfilesCommand = new RelayCommand(RefreshSaveProfilesList);
        QuickSnapshotSaveProfileCommand = new RelayCommand(QuickSnapshotSaveProfile, () => !string.IsNullOrEmpty(SelectedBaseProfileId));
        OpenSaveProfileFolderCommand = new RelayCommand<SaveProfile>(OpenSaveProfileFolder, (sp) => (sp ?? SelectedSaveProfile) != null && Directory.Exists((sp ?? SelectedSaveProfile)!.FullPath));
    }

    public string GtaSaveProfilesPath
    {
        get => _gtaSaveProfilesPath;
        set
        {
            if (SetProperty(ref _gtaSaveProfilesPath, value))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _saveProfileManager = new SaveProfileManager(value);
                    LoadSaveProfilesData();
                }
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
                CommandManager.InvalidateRequerySuggested();
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
        set
        {
            if (SetProperty(ref _selectedSaveProfile, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string NewSaveProfileName
    {
        get => _newSaveProfileName;
        set => SetProperty(ref _newSaveProfileName, value);
    }

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

    // Commands
    public ICommand BrowseSaveProfilesPathCommand { get; }
    public ICommand ActivateSaveProfileCommand { get; }
    public ICommand CreateSaveProfileCommand { get; }
    public ICommand RenameSaveProfileCommand { get; }
    public ICommand DeleteSaveProfileCommand { get; }
    public ICommand RefreshSaveProfilesCommand { get; }
    public ICommand QuickSnapshotSaveProfileCommand { get; }
    public ICommand OpenSaveProfileFolderCommand { get; }

    public void LoadSaveProfilesData()
    {
        if (_saveProfileManager == null) return;
        
        using (new ProfilerBlock(_logger, "LoadSaveProfilesData"))
        {
            BaseProfileIds = new ObservableCollection<string>(_saveProfileManager.GetBaseProfileIds());
            OnPropertyChanged(nameof(HasBaseProfiles));
            
            if (BaseProfileIds.Count > 0)
            {
                SelectedBaseProfileId = BaseProfileIds.FirstOrDefault();
            }
            else
            {
                SaveProfiles.Clear();
            }
        }
    }

    private void RefreshSaveProfilesList()
    {
        if (_saveProfileManager == null || string.IsNullOrEmpty(SelectedBaseProfileId))
        {
            SaveProfiles.Clear();
            return;
        }

        using (new ProfilerBlock(_logger, "RefreshSaveProfilesList"))
        {
            SaveProfiles = new ObservableCollection<SaveProfile>(_saveProfileManager.GetSaveProfiles(SelectedBaseProfileId));
        }
    }

    private void BrowseSaveProfilesPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select GTA IV Save Profiles Folder (usually in Documents/Rockstar Games/GTA IV/Profiles)"
        };
        
        if (dialog.ShowDialog() == true)
        {
            GtaSaveProfilesPath = dialog.FolderName;
        }
    }

    private void ActivateSaveProfile(object? param)
    {
        var profile = param as SaveProfile ?? SelectedSaveProfile;
        if (profile == null || _saveProfileManager == null || string.IsNullOrEmpty(SelectedBaseProfileId)) return;
        
        try
        {
            _saveProfileManager.ActivateSaveProfile(SelectedBaseProfileId, profile, RenameActiveSaveTo);
            RefreshSaveProfilesList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to activate save profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateSaveProfile()
    {
        if (_saveProfileManager == null || string.IsNullOrEmpty(SelectedBaseProfileId) || string.IsNullOrWhiteSpace(NewSaveProfileName)) return;
        
        try
        {
            _saveProfileManager.CreateNewSaveProfile(SelectedBaseProfileId, NewSaveProfileName.Trim(), RenameActiveSaveTo);
            NewSaveProfileName = "";
            RefreshSaveProfilesList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create save profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void QuickSnapshotSaveProfile()
    {
        if (_saveProfileManager == null || string.IsNullOrEmpty(SelectedBaseProfileId)) return;
        
        try
        {
            string suffix = string.IsNullOrWhiteSpace(QuickSnapshotName) ? "Snapshot" : QuickSnapshotName.Trim();
            
            _saveProfileManager.CloneActiveSaveProfile(SelectedBaseProfileId, suffix);
            QuickSnapshotName = "";
            RefreshSaveProfilesList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create quick snapshot: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void RenameSaveProfile(string? newName)
    {
        if (_saveProfileManager == null || SelectedSaveProfile == null || string.IsNullOrWhiteSpace(newName)) return;
        
        try
        {
            _saveProfileManager.RenameSaveProfile(SelectedSaveProfile, newName.Trim());
            RefreshSaveProfilesList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to rename save profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteSaveProfile()
    {
        if (_saveProfileManager == null || string.IsNullOrEmpty(SelectedBaseProfileId) || SelectedSaveProfile == null) return;
        
        var result = MessageBox.Show($"Are you sure you want to delete save profile '{SelectedSaveProfile.FolderName}'?\nThis cannot be undone.", 
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _saveProfileManager.DeleteSaveProfile(SelectedSaveProfile);
                RefreshSaveProfilesList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete save profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void OpenSaveProfileFolder(SaveProfile? profile)
    {
        var target = profile ?? SelectedSaveProfile;
        if (target != null && Directory.Exists(target.FullPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = target.FullPath,
                UseShellExecute = true
            });
        }
    }
}
