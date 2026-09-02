using System.IO;
using System.Security.Cryptography;
using SharpCompress.Archives;

namespace ManagerIV.Core;

/// <summary>
/// Handles verification and installation of Complete Edition runtime dependencies for Liberty's Legacy.
/// </summary>
public class LibertyTrainerDependencyService : ILibertyTrainerDependencyService
{
    private readonly BackendToolManager _backendToolManager;
    private readonly string _assetsDir;

    public LibertyTrainerDependencyService(BackendToolManager backendToolManager, string? assetsDir = null)
    {
        _backendToolManager = backendToolManager ?? throw new ArgumentNullException(nameof(backendToolManager));
        _assetsDir = assetsDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ScriptHook");
    }

    /// <inheritdoc />
    public bool CheckIsCompleteEdition(Profile? profile)
    {
        if (profile == null) return true;
        if (profile.LastKnownVersion == null) return true;
        return profile.LastKnownVersion.IsCompleteEdition;
    }

    /// <inheritdoc />
    public async Task<List<InstalledToolFile>> EnsureDependenciesAsync(
        string gamePath,
        Profile profile,
        List<InstalledToolFile> manifest,
        Func<Task>? installStandaloneUalCallback = null)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            throw new DirectoryNotFoundException($"Game directory not found: '{gamePath}'");
        }

        if (!CheckIsCompleteEdition(profile))
        {
            throw new InvalidOperationException("Liberty's Legacy Trainer requires GTA IV Complete Edition (v1.2.0.43 or later). Legacy game versions are not supported.");
        }

        var newlyInstalled = new List<InstalledToolFile>();

        // 1. Ultimate ASI Loader (UAL) Dependency
        string dinput8Path = Path.Combine(gamePath, "dinput8.dll");
        string pluginsDir = Path.Combine(gamePath, "plugins");

        bool hasFusionFix = false;
        if (Directory.Exists(pluginsDir))
        {
            hasFusionFix = File.Exists(Path.Combine(pluginsDir, "GTAIV.EFLC.FusionFix.asi")) ||
                           File.Exists(Path.Combine(pluginsDir, "GTAIV.FusionFix.asi")) ||
                           File.Exists(Path.Combine(pluginsDir, "FusionFix.asi")) ||
                           Directory.GetFiles(pluginsDir, "*FusionFix*.asi").Any();
        }

        bool hasUal = hasFusionFix || File.Exists(dinput8Path);

        if (!hasUal)
        {
            if (installStandaloneUalCallback != null)
            {
                await installStandaloneUalCallback();
            }
            else
            {
                // Standalone installation via BackendToolManager
                try
                {
                    var release = await _backendToolManager.GetLatestReleaseAsync("ThirteenAG", "Ultimate-ASI-Loader");
                    if (release.Assets != null && release.Assets.Count > 0)
                    {
                        string downloadUrl = release.DownloadUrl ?? release.Assets.Values.First();
                        string tempCache = Path.Combine(Path.GetTempPath(), "UAL_temp_" + Guid.NewGuid().ToString("N") + ".zip");
                        await _backendToolManager.DownloadToolAsync(downloadUrl, tempCache);

                        // Extract dinput8.dll
                        using (var zipStream = File.OpenRead(tempCache))
                        using (var archive = SharpCompress.Archives.ArchiveFactory.OpenArchive(zipStream))
                        {
                            var dinputEntry = archive.Entries.FirstOrDefault(e => !e.IsDirectory && !string.IsNullOrEmpty(e.Key) && Path.GetFileName(e.Key).Equals("dinput8.dll", StringComparison.OrdinalIgnoreCase));
                            if (dinputEntry != null)
                            {
                                dinputEntry.WriteToFile(dinput8Path, new SharpCompress.Common.ExtractionOptions { Overwrite = true });
                                string ualHash = await ComputeFileHashAsync(dinput8Path);
                                var toolFile = new InstalledToolFile("ASILoader", dinput8Path, ualHash);
                                manifest.Add(toolFile);
                                newlyInstalled.Add(toolFile);
                            }
                        }

                        try { File.Delete(tempCache); } catch { }
                    }
                }
                catch
                {
                    // If remote fetch fails in offline/test mode, check if dinput8 asset exists in Assets/
                    string fallbackDinput = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "MinorDependencies", "dinput8.dll");
                    if (File.Exists(fallbackDinput))
                    {
                        File.Copy(fallbackDinput, dinput8Path, overwrite: true);
                        string ualHash = await ComputeFileHashAsync(dinput8Path);
                        var toolFile = new InstalledToolFile("ASILoader", dinput8Path, ualHash);
                        manifest.Add(toolFile);
                        newlyInstalled.Add(toolFile);
                    }
                }
            }
        }

        // 2. ScriptHook.dll (Native C++ ScriptHook)
        string targetScriptHook = Path.Combine(gamePath, "ScriptHook.dll");
        if (!File.Exists(targetScriptHook))
        {
            string sourceScriptHook = Path.Combine(_assetsDir, "ScriptHook.dll");
            if (!File.Exists(sourceScriptHook))
            {
                throw new FileNotFoundException($"ScriptHook asset not found at '{sourceScriptHook}'.");
            }

            File.Copy(sourceScriptHook, targetScriptHook, overwrite: true);
            string scriptHookHash = await ComputeFileHashAsync(targetScriptHook);
            var toolFile = new InstalledToolFile("ScriptHook", targetScriptHook, scriptHookHash);
            manifest.Add(toolFile);
            newlyInstalled.Add(toolFile);
        }

        // 3. aCompleteEditionHook.asi (Complete Edition runtime compatibility fix)
        string targetCeHook = Path.Combine(gamePath, "aCompleteEditionHook.asi");
        if (!File.Exists(targetCeHook))
        {
            string sourceCeHook = Path.Combine(_assetsDir, "aCompleteEditionHook.asi");
            if (!File.Exists(sourceCeHook))
            {
                throw new FileNotFoundException($"Complete Edition Hook asset not found at '{sourceCeHook}'.");
            }

            File.Copy(sourceCeHook, targetCeHook, overwrite: true);
            string ceHookHash = await ComputeFileHashAsync(targetCeHook);
            var toolFile = new InstalledToolFile("ScriptHook", targetCeHook, ceHookHash);
            manifest.Add(toolFile);
            newlyInstalled.Add(toolFile);
        }

        return newlyInstalled;
    }

    private static async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        byte[] hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
