using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ManagerIV.Core;

/// <summary>
/// Service responsible for managing user music tracks, updating track metadata tags, and deploying shortcuts/files to Independence FM folder.
/// </summary>
public class MusicService
{
    private readonly IFileSystemLinker _linker;
    private readonly string _musicDir;
    private readonly string _tracksDir;
    private readonly string _manifestPath;
    private readonly string _userMusicPath;
    private MusicManifest _manifest;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicService"/> class.
    /// </summary>
    /// <param name="baseDir">The application's base data directory.</param>
    /// <param name="linker">The file system linker to use for deploying shortcuts.</param>
    public MusicService(string baseDir, IFileSystemLinker linker)
    {
        _linker = linker;
        _musicDir = Path.Combine(baseDir, "Music");
        _tracksDir = Path.Combine(_musicDir, "Tracks");
        _manifestPath = Path.Combine(_musicDir, "music_manifest.json");

        _userMusicPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Rockstar Games",
            "GTA IV",
            "User Music"
        );

        Directory.CreateDirectory(_musicDir);
        Directory.CreateDirectory(_tracksDir);

        if (!Directory.Exists(_userMusicPath))
        {
            Directory.CreateDirectory(_userMusicPath);
        }

        _manifest = LoadManifest();
    }

    /// <summary>
    /// Gets the Rockstar Games User Music directory path where shortcuts/files are deployed.
    /// </summary>
    public string UserMusicPath => _userMusicPath;

    /// <summary>
    /// Gets the current manifest containing all imported tracks.
    /// </summary>
    public MusicManifest Manifest => _manifest;

    private MusicManifest LoadManifest()
    {
        if (File.Exists(_manifestPath))
        {
            try
            {
                string json = File.ReadAllText(_manifestPath);
                var manifest = JsonSerializer.Deserialize<MusicManifest>(json);
                if (manifest != null)
                {
                    return manifest;
                }
            }
            catch { }
        }

        return new MusicManifest(new List<MusicTrack>());
    }

    /// <summary>
    /// Serializes and saves the current music manifest to disk.
    /// </summary>
    public void SaveManifest()
    {
        try
        {
            string json = JsonSerializer.Serialize(_manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_manifestPath, json);
        }
        catch { }
    }

    public async Task<MusicTrack?> ImportTrackAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext != ".mp3" && ext != ".wma" && ext != ".m4a") return null;

        string trackId = Guid.NewGuid().ToString();
        string destFileName = $"{trackId}{ext}";
        string destPath = Path.Combine(_tracksDir, destFileName);

        // Copy file to tracks dir
        await Task.Run(() => File.Copy(filePath, destPath, true));

        // Read metadata using TagLibSharp
        string title = Path.GetFileNameWithoutExtension(filePath);
        string artist = "Unknown Artist";
        string album = "Unknown Album";
        TimeSpan duration = TimeSpan.Zero;

        try
        {
            await Task.Run(() =>
            {
                using var tfile = TagLib.File.Create(destPath);
                if (!string.IsNullOrEmpty(tfile.Tag.Title))
                {
                    title = tfile.Tag.Title;
                }
                if (tfile.Tag.Performers != null && tfile.Tag.Performers.Length > 0)
                {
                    artist = string.Join(", ", tfile.Tag.Performers);
                }
                if (!string.IsNullOrEmpty(tfile.Tag.Album))
                {
                    album = tfile.Tag.Album;
                }
                duration = tfile.Properties.Duration;
            });
        }
        catch { }

        var track = new MusicTrack(
            trackId,
            Path.GetFileName(filePath),
            title,
            artist,
            album,
            duration,
            ext,
            true
        );

        _manifest.Tracks.Add(track);
        SaveManifest();
        return track;
    }

    public async Task UpdateTrackMetadataAsync(string trackId, string newTitle, string newArtist, string newAlbum)
    {
        var trackIndex = _manifest.Tracks.FindIndex(t => t.Id == trackId);
        if (trackIndex == -1) return;

        var oldTrack = _manifest.Tracks[trackIndex];
        var updatedTrack = oldTrack with { Title = newTitle, Artist = newArtist, Album = newAlbum };
        _manifest.Tracks[trackIndex] = updatedTrack;

        SaveManifest();

        // Update ID3 tags in the physical file
        string destFileName = $"{trackId}{oldTrack.FileExtension}";
        string filePath = Path.Combine(_tracksDir, destFileName);

        if (File.Exists(filePath))
        {
            try
            {
                await Task.Run(() =>
                {
                    using var tfile = TagLib.File.Create(filePath);
                    tfile.Tag.Title = newTitle;
                    tfile.Tag.Performers = new[] { newArtist };
                    tfile.Tag.Album = newAlbum;
                    tfile.Save();
                });
            }
            catch { }
        }
    }

    /// <summary>
    /// Deletes a track from the manifest and its associated audio file from the library directory.
    /// </summary>
    /// <param name="trackId">The unique ID of the track to delete.</param>
    public void DeleteTrack(string trackId)
    {
        var track = _manifest.Tracks.FirstOrDefault(t => t.Id == trackId);
        if (track == null) return;

        _manifest.Tracks.Remove(track);

        // Delete track file
        string destFileName = $"{trackId}{track.FileExtension}";
        string filePath = Path.Combine(_tracksDir, destFileName);
        if (File.Exists(filePath))
        {
            try { File.Delete(filePath); } catch { }
        }

        SaveManifest();
    }

    /// <summary>
    /// Toggles the enablement status of a track for deployment.
    /// </summary>
    /// <param name="trackId">The unique ID of the track.</param>
    /// <param name="isEnabled">True to enable the track, false to disable it.</param>
    public void ToggleTrackEnabled(string trackId, bool isEnabled)
    {
        var trackIndex = _manifest.Tracks.FindIndex(t => t.Id == trackId);
        if (trackIndex != -1)
        {
            _manifest.Tracks[trackIndex] = _manifest.Tracks[trackIndex] with { IsEnabled = isEnabled };
            SaveManifest();
        }
    }

    /// <summary>
    /// Reorders a track within the manifest list to a new position index.
    /// </summary>
    /// <param name="trackId">The unique ID of the track to move.</param>
    /// <param name="newIndex">The new target position index.</param>
    public void ReorderTrack(string trackId, int newIndex)
    {
        var track = _manifest.Tracks.FirstOrDefault(t => t.Id == trackId);
        if (track != null)
        {
            int oldIndex = _manifest.Tracks.IndexOf(track);
            if (oldIndex != -1 && newIndex >= 0 && newIndex < _manifest.Tracks.Count)
            {
                _manifest.Tracks.RemoveAt(oldIndex);
                _manifest.Tracks.Insert(newIndex, track);
                SaveManifest();
            }
        }
    }

    public async Task DeployMusicAsync()
    {
        // 1. Clean User Music
        if (!Directory.Exists(_userMusicPath))
        {
            Directory.CreateDirectory(_userMusicPath);
        }
        else
        {
            // Clear all files
            foreach (var file in Directory.GetFiles(_userMusicPath))
            {
                try
                {
                    File.Delete(file);
                }
                catch { }
            }
        }

        var enabledTracks = _manifest.Tracks.Where(t => t.IsEnabled).ToList();

        // Check 3 songs minimum
        if (enabledTracks.Count < 3)
        {
            throw new InvalidOperationException("Need at least 3 enabled songs for GTA IV Independence FM radio to work.");
        }

        // 2. Link files with numeric names
        for (int i = 0; i < enabledTracks.Count; i++)
        {
            var track = enabledTracks[i];
            string trackSourcePath = Path.Combine(_tracksDir, $"{track.Id}{track.FileExtension}");
            string linkPath = Path.Combine(_userMusicPath, $"{i + 1}{track.FileExtension}");

            if (File.Exists(trackSourcePath))
            {
                await Task.Run(() =>
                {
                    try
                    {
                        _linker.CreateHardLink(linkPath, trackSourcePath);
                    }
                    catch
                    {
                        // Fallback to copy if hard link fails (e.g. cross-volume documents folder)
                        File.Copy(trackSourcePath, linkPath, true);
                    }
                });
            }
        }
    }
}
