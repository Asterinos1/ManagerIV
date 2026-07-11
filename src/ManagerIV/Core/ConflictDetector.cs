using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ManagerIV.Core;

/// <summary>
/// Service responsible for scanning enabled mods and identifying file-level overrides and data merge warnings.
/// </summary>
public class ConflictDetector
{
    private static readonly string[] MergeNeededExtensions = { ".dat", ".xml", ".txt", ".ini" };

    /// <summary>
    /// Builds the conflict state for a set of enabled mods and a given load order model.
    /// Flags replacement conflicts (two mods touching the same target file) and warns if multiple mods touch handling.dat or .img files.
    /// </summary>
    public ConflictState DetectConflicts(IEnumerable<StagedMod> enabledMods, LoadOrderModel loadOrder)
    {
        if (enabledMods == null) throw new ArgumentNullException(nameof(enabledMods));
        if (loadOrder == null) throw new ArgumentNullException(nameof(loadOrder));

        var modDict = enabledMods.ToDictionary(m => m.Id);

        // Sort mods by priority: lowest priority (1) first, highest priority last.
        // This ensures higher priority values overwrite lower ones in our winner map.
        var sortedModIds = loadOrder.Entries
            .OrderBy(e => e.Priority)
            .Select(e => e.ModId)
            .Where(id => modDict.ContainsKey(id))
            .ToList();

        // Virtual Path -> List of Mod IDs that touch it, ordered by priority (increasing)
        var pathToMods = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var modId in sortedModIds)
        {
            var mod = modDict[modId];
            var orderEntry = loadOrder.Entries.First(e => e.ModId == modId);

            foreach (var file in mod.Files)
            {
                string virtualPath = GetVirtualTargetPath(orderEntry.Target, file.RelativePath);
                
                if (!pathToMods.TryGetValue(virtualPath, out var modIds))
                {
                    modIds = new List<string>();
                    pathToMods[virtualPath] = modIds;
                }
                
                if (!modIds.Contains(modId))
                {
                    modIds.Add(modId);
                }
            }
        }

        var conflicts = new Dictionary<string, ConflictInfo>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        foreach (var pair in pathToMods)
        {
            string virtualPath = pair.Key;
            var modIds = pair.Value;

            if (modIds.Count > 1)
            {
                // The winner is the first one in the list (since list is sorted in ascending priority, priority 1 wins)
                string winnerId = modIds[0];
                var loserIds = modIds.Where(id => id != winnerId).ToList();

                conflicts[virtualPath] = new ConflictInfo(virtualPath, winnerId, loserIds);

                string winnerName = modDict[winnerId].Name;
                var conflictNames = loserIds.Select(id => modDict[id].Name).ToList();

                string fileName = Path.GetFileName(virtualPath).ToLowerInvariant();
                string ext = Path.GetExtension(virtualPath).ToLowerInvariant();

                if (fileName == "handling.dat")
                {
                    warnings.Add($"Conflict on handling configuration file '{virtualPath}': '{winnerName}' overrides {string.Join(", ", conflictNames)}. These handling mods will conflict and may need manual merging.");
                }
                else if (ext == ".img")
                {
                    warnings.Add($"Conflict on archive file '{virtualPath}': '{winnerName}' overrides {string.Join(", ", conflictNames)}. Deeper merging within .img files is not supported.");
                }
                else if (MergeNeededExtensions.Contains(ext))
                {
                    warnings.Add($"Conflict on configuration/data file '{virtualPath}': '{winnerName}' overrides {string.Join(", ", conflictNames)}. These files may need manual merging.");
                }
            }
        }

        return new ConflictState(conflicts, warnings);
    }

    private string GetVirtualTargetPath(DeployTarget target, string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/').TrimStart('/');
        return target switch
        {
            DeployTarget.Update => "update/" + normalized,
            DeployTarget.Plugins => "plugins/" + Path.GetFileName(normalized),
            DeployTarget.Scripts => "scripts/" + GetPathUnderScripts(normalized),
            _ => normalized
        };
    }

    private string GetPathUnderScripts(string normalizedPath)
    {
        int scriptsIndex = normalizedPath.IndexOf("scripts/", StringComparison.OrdinalIgnoreCase);
        return scriptsIndex >= 0
            ? normalizedPath.Substring(scriptsIndex + "scripts/".Length)
            : normalizedPath;
    }
}
