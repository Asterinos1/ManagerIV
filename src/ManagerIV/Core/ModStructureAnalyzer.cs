using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;

namespace ManagerIV.Core;

/// <summary>
/// Defines a service for analyzing the internal structure and files of a mod archive.
/// </summary>
public interface IModStructureAnalyzer
{
    /// <summary>
    /// Analyzes a mod archive to determine target folders, compatibility, and configuration instructions.
    /// </summary>
    /// <param name="archivePath">The absolute path to the mod archive file.</param>
    /// <param name="toolsContext">Information about installed tools (ASI loader, FusionFix, ScriptHook).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A report detailing mod target locations, compatibility, and constraints.</returns>
    Task<ModStructureReport> AnalyzeAsync(string archivePath, InstalledToolsContext toolsContext, CancellationToken ct = default);
}

/// <summary>
/// Represents the installation status of various backend tool dependency managers/helpers.
/// </summary>
/// <param name="AsiLoaderInstalled">Whether an ASI Loader is currently installed.</param>
/// <param name="FusionFixInstalled">Whether FusionFix is currently installed.</param>
/// <param name="ScriptHookInstalled">Whether ScriptHook is currently installed.</param>
public record InstalledToolsContext(bool AsiLoaderInstalled, bool FusionFixInstalled, bool ScriptHookInstalled);

/// <summary>
/// Standard implementation of <see cref="IModStructureAnalyzer"/> for evaluating mod packaging structure.
/// </summary>
public class ModStructureAnalyzer : IModStructureAnalyzer
{
    private static readonly string[] ReadmeKeywords = new[]
    {
        "readme", "read me", "info", "install", "changelog", "credits", "notes"
    };

    private static readonly string[] ConflictKeywords = new[]
    {
        "do not use with", "incompatible with", "conflicts with", "not compatible"
    };

    public async Task<ModStructureReport> AnalyzeAsync(string archivePath, InstalledToolsContext toolsContext, CancellationToken ct = default)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Archive file not found for analysis.", archivePath);
        }

        // 2a. Inventory phase
        var allPaths = new List<string>();
        var textFileEntries = new List<IArchiveEntry>();

        using (var archive = ArchiveFactory.OpenArchive(archivePath))
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }

                string? entryKey = entry.Key;
                if (string.IsNullOrEmpty(entryKey))
                {
                    continue;
                }

                allPaths.Add(entryKey);

                // 2b. Identify text file entries for readme scan
                string filename = Path.GetFileName(entryKey);
                string filenameWithoutExt = Path.GetFileNameWithoutExtension(entryKey).ToLowerInvariant();

                if (ReadmeKeywords.Any(k => filenameWithoutExt.Contains(k)))
                {
                    textFileEntries.Add(entry);
                }
            }

            // 2b. Extract text files in-memory
            var readmeBuilder = new StringBuilder();
            foreach (var textEntry in textFileEntries)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    using (var stream = textEntry.OpenEntryStream())
                    using (var reader = new StreamReader(stream))
                    {
                        string content = await reader.ReadToEndAsync(ct);
                        if (readmeBuilder.Length > 0)
                        {
                            readmeBuilder.AppendLine();
                        }
                        readmeBuilder.AppendLine($"=== {Path.GetFileName(textEntry.Key)} ===");
                        readmeBuilder.AppendLine(content);
                    }
                }
                catch
                {
                    // Ignore transient read errors on corrupted files in archives
                }
            }

            string? rawReadmeText = readmeBuilder.Length > 0 ? readmeBuilder.ToString() : null;

            // 2c. Structural classification
            bool hasUpdateSubtree = allPaths.Any(p => {
                var norm = p.Replace('\\', '/');
                return TryGetUpdatePrefixLength(norm, out _);
            });

            bool hasLegacySubtree = allPaths.Any(p => {
                var norm = p.Replace('\\', '/');
                if (TryGetUpdatePrefixLength(norm, out _)) return false;

                return norm.StartsWith("pc/") || norm.StartsWith("common/") || norm.StartsWith("tlad/") || norm.StartsWith("tbogt/") ||
                       norm.Contains("/pc/") || norm.Contains("/common/") || norm.Contains("/tlad/") || norm.Contains("/tbogt/");
            });

            bool isMixedStructureWithUpdate = hasUpdateSubtree && hasLegacySubtree;

            var groups = new Dictionary<(DeploymentTarget Target, string Prefix), List<string>>();
            var unresolvedFiles = new List<string>();

            foreach (var path in allPaths)
            {
                DeploymentTarget target;
                string prefix;

                if (isMixedStructureWithUpdate)
                {
                    string normalizedPath = path.Replace('\\', '/');
                    if (TryGetUpdatePrefixLength(normalizedPath, out int updatePrefixLength))
                    {
                        target = DeploymentTarget.UpdateFolder;
                        prefix = path.Substring(0, updatePrefixLength);
                    }
                    else
                    {
                        target = DeploymentTarget.Unknown;
                        prefix = "";
                    }
                }
                else
                {
                    (target, prefix) = ClassifyPath(path);
                }

                if (target == DeploymentTarget.Unknown)
                {
                    unresolvedFiles.Add(path);
                }
                else
                {
                    var key = (target, prefix);
                    if (!groups.TryGetValue(key, out var list))
                    {
                        list = new List<string>();
                        groups[key] = list;
                    }
                    list.Add(path);
                }
            }

            var detectedTargets = groups.Select(kvp => new DetectedFileGroup(
                kvp.Key.Target,
                kvp.Key.Prefix,
                kvp.Value.AsReadOnly()
            )).ToList();

            // Check for Dual Target status
            bool hasUpdateGroup = groups.Keys.Any(k => k.Target == DeploymentTarget.UpdateFolder);
            bool hasNonUpdateGroup = groups.Keys.Any(k => k.Target == DeploymentTarget.PluginsFolder || k.Target == DeploymentTarget.ScriptsFolder);

            bool isDualTarget = (hasUpdateGroup && hasNonUpdateGroup) || (hasUpdateSubtree && hasLegacySubtree);

            // 2d. Readme keyword scan
            var versionCompatibility = VersionCompatibility.Unknown;
            var dependencies = new List<DependencyHint>();
            var conflictHints = new List<string>();

            if (rawReadmeText != null)
            {
                // Version Compatibility scan
                bool hasCe = ContainsAny(rawReadmeText, "complete edition", "ce", "steam", "1.2.0.4");
                bool hasLegacy = ContainsAny(rawReadmeText, "1.0.7", "1.0.8", "legacy");

                if (hasCe && hasLegacy)
                {
                    versionCompatibility = VersionCompatibility.Both;
                }
                else if (hasCe)
                {
                    versionCompatibility = VersionCompatibility.CompleteEditionOnly;
                }
                else if (hasLegacy)
                {
                    versionCompatibility = VersionCompatibility.LegacyOnly;
                }

                // Dependency scan
                if (ContainsAny(rawReadmeText, "asi loader", "ultimate asi loader"))
                {
                    dependencies.Add(new DependencyHint("Ultimate ASI Loader", toolsContext.AsiLoaderInstalled));
                }
                if (ContainsAny(rawReadmeText, "fusionfix", "fusion fix"))
                {
                    dependencies.Add(new DependencyHint("FusionFix", toolsContext.FusionFixInstalled));
                }
                if (ContainsAny(rawReadmeText, "scripthook", "script hook"))
                {
                    dependencies.Add(new DependencyHint("ScriptHook", toolsContext.ScriptHookInstalled));
                }
                if (ContainsAny(rawReadmeText, "openiv"))
                {
                    dependencies.Add(new DependencyHint("OpenIV", false));
                }

                // Conflict scan
                string[] sentences = rawReadmeText.Split(new[] { '.', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var sentence in sentences)
                {
                    string trimmed = sentence.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    if (ConflictKeywords.Any(k => trimmed.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!conflictHints.Contains(trimmed))
                        {
                            conflictHints.Add(trimmed);
                        }
                    }
                }
            }

            // Fallback for version compatibility
            if (versionCompatibility == VersionCompatibility.Unknown && hasUpdateGroup)
            {
                versionCompatibility = VersionCompatibility.CompleteEditionOnly;
            }

            return new ModStructureReport(
                detectedTargets.AsReadOnly(),
                versionCompatibility,
                dependencies.AsReadOnly(),
                conflictHints.AsReadOnly(),
                unresolvedFiles.AsReadOnly(),
                rawReadmeText,
                isDualTarget
            );
        }
    }

    private static (DeploymentTarget Target, string Prefix) ClassifyPath(string path)
    {
        string normalizedPath = path.Replace('\\', '/');

        // Rule 1: update/ subtree. Only the archive wrapper through update/ is
        // stripped; custom Fusion Overloader virtual roots below update/ stay intact.
        if (TryGetUpdatePrefixLength(normalizedPath, out int prefixLength))
        {
            string prefix = path.Substring(0, prefixLength);
            return (DeploymentTarget.UpdateFolder, prefix);
        }

        // Rule 2: plugins/ subtree
        int pluginsIndex = normalizedPath.IndexOf("plugins/", StringComparison.OrdinalIgnoreCase);
        if (pluginsIndex != -1)
        {
            string prefix = path.Substring(0, pluginsIndex + "plugins/".Length);
            return (DeploymentTarget.PluginsFolder, prefix);
        }

        // Rule 3: scripts/ subtree
        int scriptsIndex = normalizedPath.IndexOf("scripts/", StringComparison.OrdinalIgnoreCase);
        if (scriptsIndex != -1)
        {
            string prefix = path.Substring(0, scriptsIndex + "scripts/".Length);
            return (DeploymentTarget.ScriptsFolder, prefix);
        }

        // Rule 4: loose .asi or .dll at root (depth 0 or 1)
        string ext = Path.GetExtension(normalizedPath).ToLowerInvariant();
        if (ext == ".asi" || ext == ".dll")
        {
            int slashCount = normalizedPath.Count(c => c == '/');
            if (slashCount == 0)
            {
                return (DeploymentTarget.PluginsFolder, "");
            }
            if (slashCount == 1)
            {
                int firstSlash = normalizedPath.IndexOf('/');
                return (DeploymentTarget.PluginsFolder, path.Substring(0, firstSlash + 1));
            }
        }

        // Rule 5: loose .cs or .vb at root
        if (ext == ".cs" || ext == ".vb")
        {
            int slashCount = normalizedPath.Count(c => c == '/');
            if (slashCount == 0)
            {
                return (DeploymentTarget.ScriptsFolder, "");
            }
            if (slashCount == 1)
            {
                int firstSlash = normalizedPath.IndexOf('/');
                return (DeploymentTarget.ScriptsFolder, path.Substring(0, firstSlash + 1));
            }
        }

        return (DeploymentTarget.Unknown, "");
    }

    private static bool TryGetUpdatePrefixLength(string normalizedPath, out int prefixLength)
    {
        int updateIndex = normalizedPath.IndexOf("/update/", StringComparison.OrdinalIgnoreCase);
        if (updateIndex != -1)
        {
            prefixLength = updateIndex + "/update/".Length;
            return true;
        }

        if (normalizedPath.StartsWith("update/", StringComparison.OrdinalIgnoreCase))
        {
            prefixLength = "update/".Length;
            return true;
        }

        prefixLength = -1;
        return false;
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
