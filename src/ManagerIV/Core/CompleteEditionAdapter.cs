using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ManagerIV.Core;

/// <summary>
/// Deployment adapter for GTA IV Complete Edition.
/// Translates logical load order into directory junctions and hard links inside the game folder.
/// </summary>
public class CompleteEditionAdapter : IBackendAdapter
{
    private readonly string _gameDir;
    private readonly IFileSystemLinker _linker;
    private readonly TransactionJournal? _journal;
    private readonly LoadOrderService _loadOrderService;

    public CompleteEditionAdapter(string gameDir, IFileSystemLinker linker, TransactionJournal? journal = null)
    {
        _gameDir = gameDir ?? throw new ArgumentNullException(nameof(gameDir));
        _linker = linker ?? throw new ArgumentNullException(nameof(linker));
        _journal = journal;
        _loadOrderService = new LoadOrderService();
    }

    public GameVersionProfile DetectVersion(string gameDir)
    {
        string exePath = Path.Combine(gameDir, "GTAIV.exe");
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException("GTAIV.exe not found in the game directory.", exePath);
        }

        var fileInfo = new FileInfo(exePath);
        long size = fileInfo.Length;
        string hash = ComputeSha256(exePath);
        
        string version = "1.2.0.0";
        try
        {
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            version = versionInfo.FileVersion ?? "1.2.0.0";
        }
        catch
        {
            // Fallback for custom or dummy files
        }

        bool isCompleteEdition = GameVersionProfile.CheckIsCompleteEdition(version);
        return new GameVersionProfile(version, size, hash, isCompleteEdition);
    }

    public DeployTarget ResolveTarget(ModFile file)
    {
        string path = file.RelativePath.Replace('\\', '/').ToLowerInvariant();
        if (path.EndsWith(".asi"))
        {
            return DeployTarget.Plugins;
        }
        if (path.Contains("scripts/") || path.EndsWith(".dll"))
        {
            return DeployTarget.Scripts;
        }
        return DeployTarget.Update;
    }

    public async Task DeployAsync(StagedMod mod, int priority)
    {
        var target = ResolveTargetForMod(mod);
        
        if (target == DeployTarget.Update)
        {
            string folderName = _loadOrderService.GetDeployedFolderName(mod, priority);
            string junctionPath = Path.Combine(_gameDir, "update", folderName);
            string? virtualArchiveRoot = UpdateDeploymentClassifier.GetSplitUpdateVirtualArchiveRoot(mod);
            bool shouldMergeStandardRoots = UpdateDeploymentClassifier.ShouldMergeStandardUpdateRoots(mod);
            var directMergeFiles = shouldMergeStandardRoots
                ? UpdateDeploymentClassifier.GetDirectUpdateMergeFiles(mod).ToList()
                : new List<ModFile>();

            if (Path.GetFileName(junctionPath).Equals("GTAIV.EFLC.FusionFix", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Writing to reserved path '{junctionPath}' is prohibited.");
            }

            foreach (var file in directMergeFiles)
            {
                string sourcePath = Path.Combine(mod.LibraryPath, file.RelativePath);
                string targetPath = Path.Combine(_gameDir, "update", file.RelativePath);

                if (_journal != null)
                {
                    _journal.Record(new JournalEntry(JournalOpType.CreateHardLink, targetPath, sourcePath));
                }

                await Task.Run(() => _linker.CreateHardLink(targetPath, sourcePath));
            }

            if (shouldMergeStandardRoots && virtualArchiveRoot == null)
            {
                return;
            }

            string junctionTarget = virtualArchiveRoot ?? mod.LibraryPath;

            if (_journal != null)
            {
                _journal.Record(new JournalEntry(JournalOpType.CreateJunction, junctionPath, junctionTarget, IsDirectory: true));
            }
            
            await Task.Run(() => _linker.CreateJunction(junctionPath, junctionTarget));
        }
        else
        {
            // For plugins and scripts, deploy each individual file using hardlinks
            string subDirName = target == DeployTarget.Plugins ? "plugins" : "scripts";
            string targetDir = Path.Combine(_gameDir, subDirName);

            foreach (var file in mod.Files)
            {
                string libraryFilePath = Path.Combine(mod.LibraryPath, file.RelativePath);
                
                // Keep the priority prefix for .asi files to control ASI load order
                string fileName = target == DeployTarget.Plugins 
                    ? _loadOrderService.GetDeployedFileName(file, priority)
                    : Path.GetFileName(file.RelativePath);

                string targetFilePath = Path.Combine(targetDir, fileName);

                if (_journal != null)
                {
                    _journal.Record(new JournalEntry(JournalOpType.CreateHardLink, targetFilePath, libraryFilePath));
                }

                await Task.Run(() => _linker.CreateHardLink(targetFilePath, libraryFilePath));
            }
        }
    }

    public async Task UndeployAsync(StagedMod mod)
    {
        var target = ResolveTargetForMod(mod);

        if (target == DeployTarget.Update)
        {
            string updateDir = Path.Combine(_gameDir, "update");
            bool shouldMergeStandardRoots = UpdateDeploymentClassifier.ShouldMergeStandardUpdateRoots(mod);
            if (_linker.DirectoryExists(updateDir))
            {
                var directories = Directory.GetDirectories(updateDir);
                foreach (var dir in directories)
                {
                    string folderName = Path.GetFileName(dir);
                    // Match pattern: "<NNN>_ModName" where ModName matches mod.Name (without spaces/invalid chars)
                    string safeModName = string.Concat(mod.Name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "");
                    if (folderName.Length > 4 && folderName.Substring(4).Equals(safeModName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (_journal != null)
                        {
                            _journal.Record(new JournalEntry(JournalOpType.DeleteDirectory, dir, null, IsDirectory: true));
                        }
                        await Task.Run(() => _linker.DeleteDirectory(dir));
                    }
                }
            }

            if (shouldMergeStandardRoots)
            {
                foreach (var file in UpdateDeploymentClassifier.GetDirectUpdateMergeFiles(mod))
                {
                    string targetPath = Path.Combine(_gameDir, "update", file.RelativePath);
                    if (_journal != null)
                    {
                        _journal.Record(new JournalEntry(JournalOpType.DeleteFile, targetPath, null));
                    }
                    await Task.Run(() => _linker.DeleteFile(targetPath));
                }
            }
        }
        else
        {
            // Delete individual linked files
            string subDirName = target == DeployTarget.Plugins ? "plugins" : "scripts";
            string targetDir = Path.Combine(_gameDir, subDirName);

            if (_linker.DirectoryExists(targetDir))
            {
                foreach (var file in mod.Files)
                {
                    string baseName = Path.GetFileName(file.RelativePath);
                    // Look for matches in the target folder (which could be prefixed for plugins)
                    var targetFiles = Directory.GetFiles(targetDir);
                    foreach (var targetFile in targetFiles)
                    {
                        string nameOnDisk = Path.GetFileName(targetFile);
                        bool isMatch = target == DeployTarget.Plugins
                            ? nameOnDisk.EndsWith(baseName, StringComparison.OrdinalIgnoreCase) // matches e.g. "01_test.asi"
                            : nameOnDisk.Equals(baseName, StringComparison.OrdinalIgnoreCase);

                        if (isMatch)
                        {
                            if (_journal != null)
                            {
                                _journal.Record(new JournalEntry(JournalOpType.DeleteFile, targetFile, null));
                            }
                            await Task.Run(() => _linker.DeleteFile(targetFile));
                        }
                    }
                }
            }
        }
    }

    public async Task<LoadOrderModel> ReadLoadOrderAsync()
    {
        var entries = new List<LoadOrderEntry>();
        string updateDir = Path.Combine(_gameDir, "update");

        if (_linker.DirectoryExists(updateDir))
        {
            var directories = await Task.Run(() => Directory.GetDirectories(updateDir));
            foreach (var dir in directories)
            {
                string folderName = Path.GetFileName(dir);
                // Look for directories with priority prefix like "005_ModName"
                if (folderName.Length > 4 && 
                    char.IsDigit(folderName[0]) && 
                    char.IsDigit(folderName[1]) && 
                    char.IsDigit(folderName[2]) && 
                    folderName[3] == '_')
                {
                    string priorityStr = folderName.Substring(0, 3);
                    if (int.TryParse(priorityStr, out int priority))
                    {
                        string modName = folderName.Substring(4);
                        // We map Name as ModId for physical representation fallback
                        entries.Add(new LoadOrderEntry(modName, DeployTarget.Update, priority));
                    }
                }
            }
        }

        // Return ordered by priority
        return new LoadOrderModel(entries.OrderBy(e => e.Priority).ToList());
    }

    public async Task WriteLoadOrderAsync(LoadOrderModel order)
    {
        // Re-sorting physical update junctions to match the new priorities
        // In our adapter design, the actual deployment layout updates are orchestrated 
        // by undeploying and redeploying the affected junctions/hardlinks.
        await Task.CompletedTask;
    }

    public IEnumerable<string> BackendLogPaths()
    {
        return new[]
        {
            Path.Combine(_gameDir, "dinput8.log"),
            Path.Combine(_gameDir, "FusionFix.log"),
            Path.Combine(_gameDir, "ScriptHook.log"),
            Path.Combine(_gameDir, "ScriptHookDotNet.log"),
            Path.Combine(_gameDir, "GTAIV_d3d9.log")
        };
    }

    private DeployTarget ResolveTargetForMod(StagedMod mod)
    {
        if (mod.Files.Count == 0) return DeployTarget.Update;
        return ResolveTarget(mod.Files[0]);
    }

    private string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
