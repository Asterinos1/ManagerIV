using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GtaIVModLoader.Core;

/// <summary>
/// Service that manages the unified mod load order.
/// </summary>
public class LoadOrderService
{
    /// <summary>
    /// Initializes a load order list for a set of mods.
    /// </summary>
    public LoadOrderModel InitializeLoadOrder(IEnumerable<StagedMod> mods)
    {
        var entries = new List<LoadOrderEntry>();
        int priority = 1;

        foreach (var mod in mods)
        {
            var target = DetermineDeployTarget(mod);
            entries.Add(new LoadOrderEntry(mod.Id, target, priority++));
        }

        return new LoadOrderModel(entries);
    }

    /// <summary>
    /// Repositions a mod in the unified load order, shifting surrounding entries to maintain continuous priorities.
    /// </summary>
    public LoadOrderModel ReorderMod(LoadOrderModel currentModel, string modId, int targetPriority)
    {
        if (currentModel == null) throw new ArgumentNullException(nameof(currentModel));
        if (string.IsNullOrEmpty(modId)) throw new ArgumentException("Mod ID cannot be null or empty.", nameof(modId));

        var list = currentModel.Entries.ToList();
        var entryToMove = list.FirstOrDefault(e => e.ModId == modId);
        if (entryToMove == null)
        {
            return currentModel;
        }

        list.Remove(entryToMove);
        
        // Ensure bounds safety (1 to list.Count + 1)
        int resolvedPriority = Math.Clamp(targetPriority, 1, list.Count + 1);
        list.Insert(resolvedPriority - 1, entryToMove);

        // Re-sequence all priorities to be contiguous starting from 1
        var newEntries = list.Select((entry, index) => entry with { Priority = index + 1 }).ToList();
        return new LoadOrderModel(newEntries);
    }

    /// <summary>
    /// Helper to format the directory name for deployment inside update/ based on priority.
    /// E.g. "001_MyMod"
    /// </summary>
    public string GetDeployedFolderName(StagedMod mod, int priority)
    {
        string safeName = string.Concat(mod.Name.Split(Path.GetInvalidFileNameChars()));
        safeName = safeName.Replace(" ", "");
        return $"{priority:D3}_{safeName}";
    }

    /// <summary>
    /// Helper to format the deployed file name for plugins based on priority.
    /// E.g. "01_test.asi"
    /// </summary>
    public string GetDeployedFileName(ModFile file, int priority)
    {
        string fileName = Path.GetFileName(file.RelativePath);
        if (fileName.EndsWith(".asi", StringComparison.OrdinalIgnoreCase))
        {
            return $"{priority:D2}_{fileName}";
        }
        return fileName;
    }

    private DeployTarget DetermineDeployTarget(StagedMod mod)
    {
        if (mod.Files.Any(f => f.RelativePath.EndsWith(".asi", StringComparison.OrdinalIgnoreCase)))
        {
            return DeployTarget.Plugins;
        }
        if (mod.Files.Any(f => f.RelativePath.Contains("scripts", StringComparison.OrdinalIgnoreCase) || f.RelativePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            return DeployTarget.Scripts;
        }
        return DeployTarget.Update;
    }
}
