using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ManagerIV.Core;

public static class UpdateDeploymentClassifier
{
    private static readonly string[] StandardUpdateDirectories = { "pc", "common", "tbogt", "tlad" };

    public static IReadOnlyList<ModFile> GetDirectUpdateMergeFiles(StagedMod mod)
    {
        return mod.Files.Where(file =>
        {
            string rel = file.RelativePath.Replace('\\', '/');
            string[] parts = rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 &&
                   StandardUpdateDirectories.Contains(parts[0], StringComparer.OrdinalIgnoreCase) &&
                   !PathHasImgContainer(parts);
        }).ToList();
    }

    public static bool ShouldMergeStandardUpdateRoots(StagedMod mod)
    {
        var directMergeFiles = GetDirectUpdateMergeFiles(mod);
        if (directMergeFiles.Count == 0)
        {
            return false;
        }

        return !StandardRootsContainImg(mod);
    }

    public static bool IsMergeOnlyUpdateMod(StagedMod mod)
    {
        return ShouldMergeStandardUpdateRoots(mod) &&
               GetSplitUpdateVirtualArchiveRoot(mod) == null;
    }

    public static string? GetSplitUpdateVirtualArchiveRoot(StagedMod mod)
    {
        if (!Directory.Exists(mod.LibraryPath))
        {
            return null;
        }

        return Directory.GetDirectories(mod.LibraryPath)
            .Where(dir => !StandardUpdateDirectories.Contains(Path.GetFileName(dir), StringComparer.OrdinalIgnoreCase))
            .FirstOrDefault(DirectoryContainsImg);
    }

    private static bool StandardRootsContainImg(StagedMod mod)
    {
        return mod.Files
            .Select(f => f.RelativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length > 1 && StandardUpdateDirectories.Contains(parts[0], StringComparer.OrdinalIgnoreCase))
            .Any(PathHasImgContainer);
    }

    private static bool DirectoryContainsImg(string directory)
    {
        return Directory.EnumerateFiles(directory, "*.img", SearchOption.AllDirectories).Any() ||
               Directory.EnumerateDirectories(directory, "*.img", SearchOption.AllDirectories).Any();
    }

    private static bool PathHasImgContainer(string[] parts)
    {
        return parts
            .Take(Math.Max(0, parts.Length - 1))
            .Any(part => part.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
    }
}
