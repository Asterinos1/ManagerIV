using System.IO;

namespace ManagerIV.Core;

/// <summary>
/// Represents a validation issue discovered during analysis of a staged mod.
/// </summary>
public record ValidationIssue(
    string FilePath,
    string Severity, // "Warning" | "Error"
    string Message
);

/// <summary>
/// Service responsible for validating mod folder structures and file parameters against FusionOverloader rules.
/// </summary>
public class UpdateFolderValidator
{
    private const long RpfMaxFileSize = 0x7FFFFFFF; // 2 GB
    private const long ImgMaxFileSize = 0xFFFF * 2048; // ~134 MB
    private const int RscMagicValue = 0x05435352; // RSC Header Magic Value (RSC\x05)

    private static readonly string[] ResourceExtensions = { ".wft", ".wtd", ".wdr", ".wdd", ".wfv", ".wtc" };
    private static readonly string[] StandardDirectories = { "pc", "common", "tbogt", "tlad" };

    /// <summary>
    /// Validates a staged mod's file structure and properties against FusionOverloader limits and structures.
    /// </summary>
    public List<ValidationIssue> Validate(StagedMod mod)
    {
        var issues = new List<ValidationIssue>();
        if (mod == null || mod.Files.Count == 0) return issues;

        // 1. Resolve Target matching CompleteEditionAdapter logic
        DeployTarget target = DeployTarget.Update;
        if (mod.Files.Any(f => f.RelativePath.EndsWith(".asi", StringComparison.OrdinalIgnoreCase)))
        {
            target = DeployTarget.Plugins;
        }
        else if (mod.Files.Any(f => f.RelativePath.Replace('\\', '/').Contains("scripts/", StringComparison.OrdinalIgnoreCase) || 
                                   f.RelativePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            target = DeployTarget.Scripts;
        }

        // Only Update target mods are deployed via FusionOverloader and processed as archives
        if (target != DeployTarget.Update)
        {
            return issues;
        }

        bool hasArchiveDirs = false;
        bool hasPlainImgs = false;
        bool hasStandardDirs = false;
        bool hasNonStandardRootFiles = false;

        var imgBasenames = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in mod.Files)
        {
            string relPath = file.RelativePath.Replace('\\', '/');
            string fileName = Path.GetFileName(relPath);
            string ext = Path.GetExtension(relPath).ToLowerInvariant();

            // Strip sub-game prefix if present: iv/, tlad/, tbogt/
            string checkPath = relPath;
            if (checkPath.StartsWith("iv/", StringComparison.OrdinalIgnoreCase))
            {
                checkPath = checkPath.Substring(3);
            }
            else if (checkPath.StartsWith("tlad/", StringComparison.OrdinalIgnoreCase))
            {
                checkPath = checkPath.Substring(5);
            }
            else if (checkPath.StartsWith("tbogt/", StringComparison.OrdinalIgnoreCase))
            {
                checkPath = checkPath.Substring(6);
            }

            // Check for reserved path update/GTAIV.EFLC.FusionFix/
            if (checkPath.StartsWith("GTAIV.EFLC.FusionFix/", StringComparison.OrdinalIgnoreCase) || 
                checkPath.Equals("GTAIV.EFLC.FusionFix", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    file.RelativePath,
                    "Error",
                    "Writing to reserved path 'update/GTAIV.EFLC.FusionFix/' is prohibited to protect the FusionFix installation."
                ));
            }

            var parts = checkPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            string rootDir = parts[0].ToLowerInvariant();

            // Detect if path contains RPF or IMG folders (i.e., not the file itself, but a containing folder)
            string? containingImg = null;
            string? containingRpf = null;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                {
                    containingImg = parts[i];
                }
                if (parts[i].EndsWith(".rpf", StringComparison.OrdinalIgnoreCase))
                {
                    containingRpf = parts[i];
                }
            }

            if (containingImg != null)
            {
                hasArchiveDirs = true;
            }
            if (containingRpf != null)
            {
                hasArchiveDirs = true;
                issues.Add(new ValidationIssue(
                    file.RelativePath,
                    "Error",
                    $"Folder-based .rpf archive '{containingRpf}' is not supported. You must compile it into a single '.rpf' file."
                ));
            }

            if (containingImg == null && containingRpf == null)
            {
                if (ext == ".img")
                {
                    hasPlainImgs = true;
                }
                else if (StandardDirectories.Contains(rootDir) || IsCustomVirtualRoot(parts))
                {
                    hasStandardDirs = true;
                }
                else
                {
                    hasNonStandardRootFiles = true;

                    // Warn about loose model/texture assets placed in invalid folders
                    if (ext == ".wft" || ext == ".wtd" || ext == ".wdr" || ext == ".wdd" || ext == ".txd" || ext == ".dff" || ext == ".col")
                    {
                        issues.Add(new ValidationIssue(
                            file.RelativePath,
                            "Warning",
                            $"Loose asset file '{fileName}' is placed outside any .img or .rpf archive. It will be ignored by the engine."
                        ));
                    }
                }
            }

            // Checks within IMG folders
            if (containingImg != null)
            {
                // File size limit for IMG
                if (file.SizeBytes > ImgMaxFileSize)
                {
                    issues.Add(new ValidationIssue(
                        file.RelativePath,
                        "Error",
                        $"File '{fileName}' exceeds the IMG limit of 134 MB (Current: {file.SizeBytes / (1024.0 * 1024.0):F1} MB) and will be skipped."
                    ));
                }

                // Basename collision tracking
                if (!imgBasenames.TryGetValue(fileName, out var paths))
                {
                    paths = new List<string>();
                    imgBasenames[fileName] = paths;
                }
                paths.Add(file.RelativePath);

                // Filename length warnings
                if (fileName.Length > 255)
                {
                    issues.Add(new ValidationIssue(
                        file.RelativePath,
                        "Warning",
                        $"Filename '{fileName}' exceeds 255 characters and will be truncated by the loader."
                    ));
                }
                else if (fileName.Length > 23)
                {
                    issues.Add(new ValidationIssue(
                        file.RelativePath,
                        "Warning",
                        $"Filename '{fileName}' is {fileName.Length} characters. It will be truncated if compiled to an IMG v2 archive."
                    ));
                }

                // RSC Magic Header verification
                if (ResourceExtensions.Contains(ext))
                {
                    string fullPath = Path.Combine(mod.LibraryPath, file.RelativePath);
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            if (fs.Length >= 4)
                            {
                                byte[] header = new byte[4];
                                int read = fs.Read(header, 0, 4);
                                if (read == 4)
                                {
                                    int magic = BitConverter.ToInt32(header, 0);
                                    if (magic != RscMagicValue)
                                    {
                                        issues.Add(new ValidationIssue(
                                            file.RelativePath,
                                            "Warning",
                                            $"Resource '{fileName}' lacks a valid RSC header. It may package incorrectly."
                                        ));
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Skip locked files
                        }
                    }
                }
            }

            // Checks within RPF folders
            if (containingRpf != null)
            {
                // File size limit for RPF
                if (file.SizeBytes > RpfMaxFileSize)
                {
                    issues.Add(new ValidationIssue(
                        file.RelativePath,
                        "Error",
                        $"File '{fileName}' exceeds the RPF limit of 2 GB and will be skipped."
                    ));
                }

                // Filename length limits
                if (fileName.Length > 255)
                {
                    issues.Add(new ValidationIssue(
                        file.RelativePath,
                        "Error",
                        $"Filename '{fileName}' exceeds the 255-character RPF limit."
                    ));
                }
            }
        }

        // Flag duplicate basenames within IMG archives
        foreach (var pair in imgBasenames)
        {
            if (pair.Value.Count > 1)
            {
                issues.Add(new ValidationIssue(
                    pair.Key,
                    "Warning",
                    $"Duplicate filename '{pair.Key}' detected inside IMG folders at: {string.Join(", ", pair.Value)}. Subdirectories are ignored, causing entry collisions."
                ));
            }
        }

        // General structure compatibility validation
        bool isCompatible = hasArchiveDirs || hasPlainImgs || hasStandardDirs;
        if (!isCompatible && hasNonStandardRootFiles)
        {
            issues.Add(new ValidationIssue(
                mod.Name,
                "Warning",
                "No .img/.rpf folders, plain .img files, or standard folders (pc/, common/) found. FusionOverloader may not load these files."
            ));
        }
        else if (isCompatible && hasNonStandardRootFiles)
        {
            issues.Add(new ValidationIssue(
                mod.Name,
                "Warning",
                "Some files reside outside standard game subfolders and will be ignored by FusionOverloader."
            ));
        }

        return issues;
    }

    private static bool IsCustomVirtualRoot(string[] parts)
    {
        return parts.Length > 1 && !parts[0].Contains('.', StringComparison.Ordinal);
    }
}
