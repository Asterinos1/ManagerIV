using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ManagerIV.Core;

namespace ManagerIV.Tests;

public class MusicTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly string _userMusicDir;
    private readonly NativeFileSystemLinker _linker;
    private readonly MusicService _musicService;

    public MusicTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "ManagerIVMusicTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testBaseDir);

        _linker = new NativeFileSystemLinker();
        _musicService = new MusicService(_testBaseDir, _linker);

        // Override user music path for testing to avoid touching user documents folder
        var field = typeof(MusicService).GetField("_userMusicPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        _userMusicDir = Path.Combine(_testBaseDir, "UserMusic");
        Directory.CreateDirectory(_userMusicDir);
        field?.SetValue(_musicService, _userMusicDir);
    }

    [Fact]
    public async Task TestTrackImportAndMetadataFallback()
    {
        // Arrange
        string sourceSongPath = Path.Combine(_testBaseDir, "mysong.mp3");
        File.WriteAllText(sourceSongPath, "dummy mp3 data");

        // Act
        var track = await _musicService.ImportTrackAsync(sourceSongPath);

        // Assert
        Assert.NotNull(track);
        Assert.Equal("mysong", track.Title); // Fallback to filename
        Assert.Equal("Unknown Artist", track.Artist);
        Assert.Equal(".mp3", track.FileExtension);
        Assert.True(track.IsEnabled);
        Assert.True(File.Exists(Path.Combine(_testBaseDir, "Music", "Tracks", $"{track.Id}.mp3")));
    }

    [Fact]
    public async Task TestTrackToggling()
    {
        // Arrange
        string sourceSongPath = Path.Combine(_testBaseDir, "song.mp3");
        File.WriteAllText(sourceSongPath, "dummy mp3 data");
        var track = await _musicService.ImportTrackAsync(sourceSongPath);
        Assert.NotNull(track);
        Assert.True(_musicService.Manifest.Tracks[0].IsEnabled);

        // Act
        _musicService.ToggleTrackEnabled(track.Id, false);
        Assert.False(_musicService.Manifest.Tracks[0].IsEnabled);

        _musicService.ToggleTrackEnabled(track.Id, true);
        Assert.True(_musicService.Manifest.Tracks[0].IsEnabled);
    }

    [Fact]
    public async Task TestDeploymentValidationAndExecution()
    {
        // Arrange
        string path1 = Path.Combine(_testBaseDir, "track_1.mp3");
        File.WriteAllText(path1, "dummy mp3 data 1");
        var track1 = await _musicService.ImportTrackAsync(path1);

        // Act & Assert - Less than 3 tracks should fail validation
        await Assert.ThrowsAsync<InvalidOperationException>(() => _musicService.DeployMusicAsync());

        // Import 2 more songs to reach 3 total enabled
        for (int i = 2; i <= 3; i++)
        {
            string path = Path.Combine(_testBaseDir, $"track_{i}.mp3");
            File.WriteAllText(path, $"dummy mp3 data {i}");
            var track = await _musicService.ImportTrackAsync(path);
            Assert.NotNull(track);
        }

        // Deploy music
        await _musicService.DeployMusicAsync();

        // Verify files deployed with numeric names
        Assert.True(File.Exists(Path.Combine(_userMusicDir, "1.mp3")));
        Assert.True(File.Exists(Path.Combine(_userMusicDir, "2.mp3")));
        Assert.True(File.Exists(Path.Combine(_userMusicDir, "3.mp3")));

        // Disable track 1 and attempt deployment (now 2 tracks, should fail)
        _musicService.ToggleTrackEnabled(track1!.Id, false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _musicService.DeployMusicAsync());
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
}
