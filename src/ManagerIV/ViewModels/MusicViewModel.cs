using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ManagerIV.Core;
using Microsoft.Win32;

namespace ManagerIV.ViewModels;

public class MusicViewModel : ViewModelBase
{
    private readonly MusicService _musicService;
    private MusicTrack? _selectedTrack;
    private string _statusText = "Ready";
    private string _editingTitle = "";
    private string _editingArtist = "";
    private string _editingAlbum = "";
    private bool _isBusy;
    private bool _isEditingTrack;

    public MusicViewModel(string baseDir, IFileSystemLinker linker)
    {
        _musicService = new MusicService(baseDir, linker);

        AllTracks = new ObservableCollection<MusicTrack>(_musicService.Manifest.Tracks);

        // Commands
        ImportTracksCommand = new RelayCommand(async () => await ImportTracksAsync());
        DeleteTrackCommand = new RelayCommand<MusicTrack>(DeleteTrack, t => t != null);
        ToggleTrackEnabledCommand = new RelayCommand<MusicTrack>(ToggleTrackEnabled, t => t != null);
        MoveTrackUpCommand = new RelayCommand<MusicTrack>(MoveTrackUp, t => t != null);
        MoveTrackDownCommand = new RelayCommand<MusicTrack>(MoveTrackDown, t => t != null);
        EditTrackCommand = new RelayCommand<MusicTrack>(track =>
        {
            if (track != null)
            {
                SelectedTrack = track;
                IsEditingTrack = true;
            }
        });
        CloseEditTrackCommand = new RelayCommand(() =>
        {
            IsEditingTrack = false;
        });
        SaveTrackMetadataCommand = new RelayCommand(async () => await SaveTrackMetadataAsync(), () => SelectedTrack != null);
        DeployMusicCommand = new RelayCommand(async () => await DeployMusicAsync());
        ClearMusicCommand = new RelayCommand(ClearMusic);
    }

    public ObservableCollection<MusicTrack> AllTracks { get; }

    public MusicTrack? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            if (SetProperty(ref _selectedTrack, value))
            {
                EditingTitle = value?.Title ?? "";
                EditingArtist = value?.Artist ?? "";
                EditingAlbum = value?.Album ?? "";
            }
        }
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

    public string EditingTitle
    {
        get => _editingTitle;
        set => SetProperty(ref _editingTitle, value);
    }

    public string EditingArtist
    {
        get => _editingArtist;
        set => SetProperty(ref _editingArtist, value);
    }

    public string EditingAlbum
    {
        get => _editingAlbum;
        set => SetProperty(ref _editingAlbum, value);
    }

    public bool IsEditingTrack
    {
        get => _isEditingTrack;
        set => SetProperty(ref _isEditingTrack, value);
    }

    public string TracksCountText
    {
        get
        {
            int count = AllTracks.Count(t => t.IsEnabled);
            return $"{count} enabled song{(count == 1 ? "" : "s")} (Need at least 3 for Independence FM)";
        }
    }

    public bool NeedsMoreTracks => AllTracks.Count(t => t.IsEnabled) < 3;

    // Commands
    public ICommand ImportTracksCommand { get; }
    public ICommand DeleteTrackCommand { get; }
    public ICommand ToggleTrackEnabledCommand { get; }
    public ICommand MoveTrackUpCommand { get; }
    public ICommand MoveTrackDownCommand { get; }
    public ICommand EditTrackCommand { get; }
    public ICommand CloseEditTrackCommand { get; }
    public ICommand SaveTrackMetadataCommand { get; }
    public ICommand DeployMusicCommand { get; }
    public ICommand ClearMusicCommand { get; }

    private async Task ImportTracksAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Audio Files (*.mp3;*.wma;*.m4a)|*.mp3;*.wma;*.m4a"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            await ImportFilesAsync(openFileDialog.FileNames);
        }
    }

    public async Task ImportFilesAsync(string[] files)
    {
        IsBusy = true;
        StatusText = "Importing songs...";
        int count = 0;
        foreach (var file in files)
        {
            try
            {
                var track = await _musicService.ImportTrackAsync(file);
                if (track != null)
                {
                    AllTracks.Add(track);
                    count++;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing {Path.GetFileName(file)}: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        IsBusy = false;
        StatusText = $"Successfully imported {count} song(s).";
        RefreshCounts();
    }

    private void DeleteTrack(MusicTrack t)
    {
        if (t == null) return;
        _musicService.DeleteTrack(t.Id);
        AllTracks.Remove(t);
        if (SelectedTrack?.Id == t.Id)
        {
            SelectedTrack = null;
        }
        StatusText = $"Deleted track '{t.Title}'.";
        RefreshCounts();
    }

    private void ToggleTrackEnabled(MusicTrack t)
    {
        if (t == null) return;
        bool newState = !t.IsEnabled;
        _musicService.ToggleTrackEnabled(t.Id, newState);
        
        var trackIdx = AllTracks.IndexOf(t);
        if (trackIdx != -1)
        {
            var updated = t with { IsEnabled = newState };
            AllTracks[trackIdx] = updated;
            if (SelectedTrack?.Id == t.Id) SelectedTrack = updated;
        }
        RefreshCounts();
    }

    private void MoveTrackUp(MusicTrack t)
    {
        if (t == null) return;
        int idx = AllTracks.IndexOf(t);
        if (idx > 0)
        {
            _musicService.ReorderTrack(t.Id, idx - 1);
            AllTracks.Move(idx, idx - 1);
        }
    }

    private void MoveTrackDown(MusicTrack t)
    {
        if (t == null) return;
        int idx = AllTracks.IndexOf(t);
        if (idx < AllTracks.Count - 1)
        {
            _musicService.ReorderTrack(t.Id, idx + 1);
            AllTracks.Move(idx, idx + 1);
        }
    }

    // Needed for drag-and-drop reordering from view
    public void ReorderTrack(MusicTrack track, int newIndex)
    {
        if (track == null || newIndex < 0 || newIndex >= AllTracks.Count) return;
        
        int oldIndex = AllTracks.IndexOf(track);
        if (oldIndex != -1 && oldIndex != newIndex)
        {
            _musicService.ReorderTrack(track.Id, newIndex);
            AllTracks.Move(oldIndex, newIndex);
        }
    }

    private async Task SaveTrackMetadataAsync()
    {
        if (SelectedTrack == null) return;
        IsBusy = true;
        StatusText = "Saving metadata...";

        await _musicService.UpdateTrackMetadataAsync(SelectedTrack.Id, EditingTitle, EditingArtist, EditingAlbum);

        var trackIdx = AllTracks.IndexOf(SelectedTrack);
        var updated = SelectedTrack with { Title = EditingTitle, Artist = EditingArtist, Album = EditingAlbum };
        if (trackIdx != -1) AllTracks[trackIdx] = updated;

        SelectedTrack = updated;
        IsEditingTrack = false;
        IsBusy = false;
        StatusText = $"Saved metadata for '{EditingTitle}'.";
    }

    private async Task DeployMusicAsync()
    {
        if (NeedsMoreTracks)
        {
            MessageBox.Show("Independence FM requires at least 3 enabled songs to function properly. Please add and enable more songs first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        StatusText = $"Deploying enabled tracks to Rockstar Games User Music...";

        try
        {
            await _musicService.DeployMusicAsync();
            StatusText = $"Successfully deployed user music.";
        }
        catch (Exception ex)
        {
            StatusText = $"Deployment failed: {ex.Message}";
            MessageBox.Show($"Failed to deploy music: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearMusic()
    {
        if (!AllTracks.Any()) return;

        var result = MessageBox.Show(
            "Are you sure you want to permanently delete all tracks from the music library?\n\nThis will remove them from Independence FM and delete the audio files from your music library. This action CANNOT be undone.", 
            "Clear Music Library", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            var tracks = AllTracks.ToList();
            foreach (var t in tracks)
            {
                _musicService.DeleteTrack(t.Id);
            }
            AllTracks.Clear();
            SelectedTrack = null;
            StatusText = "Cleared music library.";
            RefreshCounts();
        }
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(TracksCountText));
        OnPropertyChanged(nameof(NeedsMoreTracks));
    }
}
