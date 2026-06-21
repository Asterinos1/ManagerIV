using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ManagerIV.Core;

/// <summary>
/// Contains metadata resolved by scanning a mod archive and its filename.
/// </summary>
public record ModMetadata(
    string Name,
    string Version,
    string Description,
    string Compatibility,
    IReadOnlyList<string> LoaderRequirements,
    IReadOnlyList<string> FileManifest
);

/// <summary>
/// Service responsible for scanning files to detect game version compatibility, 
/// determining loader requirements, and parsing filenames for name and version.
/// </summary>
public class MetadataService
{
    private static readonly Regex NoiseRegex = new(
        @"[_\-\s]*(?:final|release|fix|build[_\-\s]*[a-f0-9]+|[a-f0-9]{7,8})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex VersionRegex = new(
        @"(?:[_\-\s]+v?|v|(?<=[a-zA-Z])v?)(\d+(?:\.\d+)+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex CeRegex = new(
        @"\b(Complete\s+Edition|CE|1\.2\.0|FusionFix|FusionOverloader)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex LegacyRegex = new(
        @"\b(1\.0\.4\.0|1\.0\.7\.0|1\.0\.8\.0|downgrade|GFWL)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    /// <summary>
    /// Parses a filename to extract the Mod Name and Version.
    /// </summary>
    public (string Name, string Version) ParseFilename(string fileName)
    {
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // Strip trailing noise (e.g. final, release, build hashes)
        string cleanedName = NoiseRegex.Replace(nameWithoutExtension, "");

        // Extract version token
        var match = VersionRegex.Match(cleanedName);
        string version = "1.0.0";
        string baseName = cleanedName;

        if (match.Success)
        {
            version = match.Groups[1].Value;
            baseName = cleanedName.Substring(0, match.Index);
        }

        // Strip any newly uncovered trailing noise in the base name
        baseName = NoiseRegex.Replace(baseName, "");

        // Normalize separators: replace _, -, and . with spaces
        baseName = baseName.Replace('_', ' ').Replace('-', ' ').Replace('.', ' ').Trim();
        while (baseName.Contains("  "))
        {
            baseName = baseName.Replace("  ", " ");
        }

        // Title case: capitalize first letter of each word, preserving internal casing
        var words = baseName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
            }
        }

        string parsedName = string.Join(" ", words);
        if (string.IsNullOrWhiteSpace(parsedName))
        {
            parsedName = nameWithoutExtension;
        }

        return (parsedName, version);
    }

    /// <summary>
    /// Scans the contents of an extracted mod directory to infer compatibility, 
    /// loader requirements, and build a file manifest.
    /// </summary>
    public ModMetadata ScanExtractedDirectory(string dirPath, string fileName)
    {
        if (!Directory.Exists(dirPath))
        {
            throw new DirectoryNotFoundException($"Extracted directory not found at '{dirPath}'.");
        }

        var (parsedName, parsedVersion) = ParseFilename(fileName);

        var allFiles = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
        var relativeManifest = new List<string>();

        bool hasAsi = false;
        bool hasScriptHook = false;
        bool hasNetScript = false;

        bool hasCeKeywords = false;
        bool hasLegacyKeywords = false;

        foreach (var file in allFiles)
        {
            // Normalize path separator to forward slash for clean manifest
            string relative = Path.GetRelativePath(dirPath, file);
            relativeManifest.Add(relative.Replace('\\', '/'));

            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".asi")
            {
                hasAsi = true;
            }
            else if (file.Contains("scripthook", StringComparison.OrdinalIgnoreCase))
            {
                hasScriptHook = true;
            }

            if (ext == ".dll" && file.Contains("scripts", StringComparison.OrdinalIgnoreCase))
            {
                hasNetScript = true;
            }

            // Inspect contents of text/ini files for compatibility indicators
            if (ext == ".txt" || ext == ".ini" || ext == ".md" || Path.GetFileNameWithoutExtension(file).Contains("readme", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var info = new FileInfo(file);
                    // Avoid scanning massive files to preserve performance (limit to 200KB)
                    if (info.Length < 200 * 1024)
                    {
                        string content = File.ReadAllText(file);
                        if (CeRegex.IsMatch(content))
                        {
                            hasCeKeywords = true;
                        }
                        if (LegacyRegex.IsMatch(content))
                        {
                            hasLegacyKeywords = true;
                        }
                    }
                }
                catch
                {
                    // Ignore transient file locking or read errors
                }
            }
        }

        // Determine compatibility status
        string compatibility = "Unspecified";
        if (hasCeKeywords && hasLegacyKeywords)
        {
            compatibility = "Mixed";
        }
        else if (hasCeKeywords)
        {
            compatibility = "CE-compatible";
        }
        else if (hasLegacyKeywords)
        {
            compatibility = "Legacy";
        }

        // Deduce loader requirements
        var loaderRequirements = new List<string>();
        if (hasAsi)
        {
            loaderRequirements.Add("ASI Loader");
        }
        if (hasScriptHook)
        {
            loaderRequirements.Add("ScriptHook");
        }
        if (hasNetScript)
        {
            loaderRequirements.Add("ScriptHookDotNet");
        }

        return new ModMetadata(
            parsedName,
            parsedVersion,
            $"Imported from {fileName}.",
            compatibility,
            loaderRequirements,
            relativeManifest
        );
    }
}
