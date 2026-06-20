using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace GtaIVModLoader.Core;

/// <summary>
/// Status of the game version verification.
/// </summary>
public enum WatchdogStatus
{
    Match,
    Mismatch,
    MissingExecutable,
    NoLastKnownState
}

/// <summary>
/// Details returned by the UpdateWatchdog verification.
/// </summary>
public record WatchdogResult(
    WatchdogStatus Status,
    GameVersionProfile? CurrentProfile,
    string Message
);

/// <summary>
/// Watchdog service that checks if GTAIV.exe has changed under the user, raising compatibility warnings.
/// </summary>
public class UpdateWatchdog
{
    /// <summary>
    /// Computes the version profile (file version, size, SHA-256 hash) for the GTAIV.exe in the specified directory.
    /// </summary>
    public async Task<GameVersionProfile> CaptureCurrentVersionAsync(string gameDir)
    {
        string exePath = Path.Combine(gameDir, "GTAIV.exe");
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException("GTAIV.exe not found in the game directory.", exePath);
        }

        var fileInfo = new FileInfo(exePath);
        long size = fileInfo.Length;
        string hash = await ComputeSha256Async(exePath);

        string version = "1.2.0.0";
        try
        {
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            version = versionInfo.FileVersion ?? "1.2.0.0";
        }
        catch
        {
            // Standard fallback if file version metadata is missing (e.g. dummy test file)
        }

        bool isCompleteEdition = version.StartsWith("1.2.") || size > 50 * 1024 * 1024;
        return new GameVersionProfile(version, size, hash, isCompleteEdition);
    }

    /// <summary>
    /// Compares the current GTAIV.exe against the state stored in the active profile.
    /// </summary>
    public async Task<WatchdogResult> VerifyGameVersionAsync(string gameDir, Profile activeProfile)
    {
        if (activeProfile == null) throw new ArgumentNullException(nameof(activeProfile));

        string exePath = Path.Combine(gameDir, "GTAIV.exe");
        if (!File.Exists(exePath))
        {
            return new WatchdogResult(WatchdogStatus.MissingExecutable, null, "GTAIV.exe is missing from the game directory.");
        }

        var current = await CaptureCurrentVersionAsync(gameDir);

        if (activeProfile.LastKnownVersion == null)
        {
            return new WatchdogResult(WatchdogStatus.NoLastKnownState, current, "No last known game version is stored in the active profile.");
        }

        var last = activeProfile.LastKnownVersion;
        if (current.ExecutableSize != last.ExecutableSize || 
            current.Version != last.Version || 
            !string.Equals(current.ExecutableHash, last.ExecutableHash, StringComparison.OrdinalIgnoreCase))
        {
            string msg = $"GTAIV.exe changed (was version {last.Version}, size {last.ExecutableSize} bytes; now version {current.Version}, size {current.ExecutableSize} bytes). Your backend tools and some mods may no longer load.";
            return new WatchdogResult(WatchdogStatus.Mismatch, current, msg);
        }

        return new WatchdogResult(WatchdogStatus.Match, current, "Game version matches the profile state.");
    }

    private async Task<string> ComputeSha256Async(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        byte[] hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
