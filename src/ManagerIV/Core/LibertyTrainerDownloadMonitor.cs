using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;

namespace ManagerIV.Core;

/// <summary>
/// Monitors the user's Downloads directory for a completed Liberty's Legacy ZIP file.
/// </summary>
public class LibertyTrainerDownloadMonitor : ILibertyTrainerDownloadMonitor
{
    private static readonly Guid FolderDownloads = new("374DE290-123F-4565-9164-39C4925E467B");

    private static readonly string[] IncompleteExtensions = new[]
    {
        ".crdownload", ".part", ".tmp", ".download", ".opdownload", ".partial"
    };

    private readonly ILibertyTrainerValidator _validator;

    public LibertyTrainerDownloadMonitor(ILibertyTrainerValidator validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);

    /// <inheritdoc />
    public string GetDownloadsDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                int hr = SHGetKnownFolderPath(FolderDownloads, 0, IntPtr.Zero, out IntPtr pathPtr);
                if (hr == 0 && pathPtr != IntPtr.Zero)
                {
                    try
                    {
                        string? path = Marshal.PtrToStringUni(pathPtr);
                        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                        {
                            return path;
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pathPtr);
                    }
                }
            }
            catch
            {
                // Fallback to user profile path
            }
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string fallback = Path.Combine(userProfile, "Downloads");
        return fallback;
    }

    /// <inheritdoc />
    public async Task<string?> WaitForCandidateArchiveAsync(
        string downloadsDir,
        DateTime startTime,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(downloadsDir) || !Directory.Exists(downloadsDir))
        {
            return null;
        }

        var candidateQueue = new ConcurrentQueue<string>();
        var rejectedPaths = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        // Pre-populate any existing zip files created/modified around workflow start
        try
        {
            var existingZips = Directory.GetFiles(downloadsDir, "*.zip");
            foreach (var zip in existingZips)
            {
                if (IsCreatedOrModifiedAfter(zip, startTime))
                {
                    candidateQueue.Enqueue(zip);
                }
            }
        }
        catch { }

        using var watcher = new FileSystemWatcher(downloadsDir)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            Filter = "*.*",
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        FileSystemEventHandler onChangeOrCreated = (s, e) =>
        {
            string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
            if (ext == ".zip")
            {
                candidateQueue.Enqueue(e.FullPath);
            }
        };

        RenamedEventHandler onRenamed = (s, e) =>
        {
            string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
            if (ext == ".zip")
            {
                candidateQueue.Enqueue(e.FullPath);
            }
        };

        watcher.Created += onChangeOrCreated;
        watcher.Changed += onChangeOrCreated;
        watcher.Renamed += onRenamed;

        try
        {
            progress?.Report($"Watching '{downloadsDir}' for trainer archive download...");

            while (!cancellationToken.IsCancellationRequested)
            {
                // Also scan directory periodically in case an event was missed
                try
                {
                    var currentZips = Directory.GetFiles(downloadsDir, "*.zip");
                    foreach (var zip in currentZips)
                    {
                        if (!rejectedPaths.ContainsKey(zip) && IsCreatedOrModifiedAfter(zip, startTime))
                        {
                            candidateQueue.Enqueue(zip);
                        }
                    }
                }
                catch { }

                while (candidateQueue.TryDequeue(out var candidate))
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    if (rejectedPaths.ContainsKey(candidate)) continue;

                    string ext = Path.GetExtension(candidate).ToLowerInvariant();
                    if (IncompleteExtensions.Contains(ext)) continue;
                    if (ext != ".zip") continue;

                    if (!File.Exists(candidate)) continue;

                    // Check if candidate is stable & completely written by browser
                    bool isStable = await WaitForFileStabilityAsync(candidate, cancellationToken);
                    if (!isStable) continue;

                    progress?.Report($"Inspecting archive: {Path.GetFileName(candidate)}...");

                    // Validate archive structure
                    var validation = _validator.ValidateArchive(candidate);
                    if (validation.IsValid)
                    {
                        progress?.Report($"Valid Liberty's Legacy package detected: {Path.GetFileName(candidate)}");
                        return candidate;
                    }
                    else
                    {
                        // Mark as rejected so we do not re-check every tick
                        rejectedPaths.TryAdd(candidate, 0);
                        progress?.Report($"Skipping non-trainer ZIP: {Path.GetFileName(candidate)} ({validation.ErrorMessage})");
                    }
                }

                await Task.Delay(500, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= onChangeOrCreated;
            watcher.Changed -= onChangeOrCreated;
            watcher.Renamed -= onRenamed;
        }

        return null;
    }

    private static bool IsCreatedOrModifiedAfter(string filePath, DateTime startTime)
    {
        try
        {
            var creationTime = File.GetCreationTimeUtc(filePath);
            var writeTime = File.GetLastWriteTimeUtc(filePath);
            var threshold = startTime.ToUniversalTime().Subtract(TimeSpan.FromSeconds(5));

            return creationTime >= threshold || writeTime >= threshold;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> WaitForFileStabilityAsync(string filePath, CancellationToken cancellationToken)
    {
        const int maxAttempts = 6;
        for (int i = 0; i < maxAttempts; i++)
        {
            if (cancellationToken.IsCancellationRequested) return false;
            if (!File.Exists(filePath)) return false;

            try
            {
                long len1 = new FileInfo(filePath).Length;
                if (len1 <= 0)
                {
                    await Task.Delay(400, cancellationToken);
                    continue;
                }

                await Task.Delay(400, cancellationToken);
                long len2 = new FileInfo(filePath).Length;

                if (len1 == len2)
                {
                    // Attempt exclusive open to ensure writer has completed and released file
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    return true;
                }
            }
            catch (IOException)
            {
                // File locked by downloading process, retry next loop
                await Task.Delay(500, cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(500, cancellationToken);
            }
        }

        return false;
    }
}
