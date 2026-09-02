using System.IO;
using System.Text.RegularExpressions;
using SharpCompress.Archives;

namespace ManagerIV.Core;

/// <summary>
/// Implements structural and security validation for Liberty's Legacy trainer packages.
/// </summary>
public class LibertyTrainerValidator : ILibertyTrainerValidator
{
    public const int MaxEntries = 5000;
    public const long MaxUncompressedBytes = 500L * 1024 * 1024; // 500 MB limit

    private static readonly string[] WindowsReservedDeviceNames = new[]
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly Regex VersionRegex = new(
        @"(?:v|version|trainer\s*)?(\d+\.\d+(?:\.\d+)*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    /// <inheritdoc />
    public TrainerValidationResult ValidateArchive(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            return new TrainerValidationResult(false, $"Archive file not found: {archivePath}");
        }

        try
        {
            using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = ValidateArchiveStream(stream);
            if (result.IsValid && string.IsNullOrEmpty(result.DetectedVersion))
            {
                string fileName = Path.GetFileNameWithoutExtension(archivePath);
                var match = VersionRegex.Match(fileName);
                if (match.Success)
                {
                    result = result with { DetectedVersion = match.Groups[1].Value };
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            return new TrainerValidationResult(false, $"Failed to open or read archive: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public TrainerValidationResult ValidateArchiveStream(Stream stream)
    {
        if (stream == null || !stream.CanRead)
        {
            return new TrainerValidationResult(false, "Invalid or unreadable stream.");
        }

        try
        {
            using var archive = ArchiveFactory.OpenArchive(stream);
            return ValidateArchiveInternal(archive);
        }
        catch (Exception ex)
        {
            return new TrainerValidationResult(false, $"Archive format validation error: {ex.Message}");
        }
    }

    private TrainerValidationResult ValidateArchiveInternal(IArchive archive)
    {
        int entryCount = 0;
        long totalUncompressedBytes = 0;
        var seenNormalizedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileEntries = new List<(string Key, string NormalizedKey, long Size)>();

        foreach (var entry in archive.Entries)
        {
            entryCount++;
            if (entryCount > MaxEntries)
            {
                return new TrainerValidationResult(false, $"Archive exceeds maximum allowed entry count limit ({MaxEntries}).");
            }

            if (entry.IsEncrypted)
            {
                return new TrainerValidationResult(false, "Encrypted entries are not allowed in trainer archives.");
            }

            string? rawKey = entry.Key;
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                if (entry.IsDirectory) continue;
                return new TrainerValidationResult(false, "Archive contains an entry with an empty or whitespace name.");
            }

            // Path Traversal Security Checks
            if (rawKey.Contains("..") || rawKey.StartsWith('/') || rawKey.StartsWith('\\') || Path.IsPathRooted(rawKey))
            {
                return new TrainerValidationResult(false, $"Potential path traversal detected in entry: '{rawKey}'.");
            }

            // Alternate Data Streams & Drive Letters
            if (rawKey.Contains(':'))
            {
                return new TrainerValidationResult(false, $"Illegal character or stream separator ':' detected in entry: '{rawKey}'.");
            }

            // Normalize path separators
            string normalized = rawKey.Replace('\\', '/').TrimStart('/');
            string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Any(s => s == ".." || s == "."))
            {
                return new TrainerValidationResult(false, $"Invalid relative path segments detected in entry: '{rawKey}'.");
            }

            // Windows Device Name Validation
            foreach (var segment in segments)
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(segment);
                if (WindowsReservedDeviceNames.Contains(segment, StringComparer.OrdinalIgnoreCase) ||
                    WindowsReservedDeviceNames.Contains(nameWithoutExt, StringComparer.OrdinalIgnoreCase))
                {
                    return new TrainerValidationResult(false, $"Entry uses a Windows reserved device name '{segment}': '{rawKey}'.");
                }
            }

            if (!entry.IsDirectory)
            {
                // Total uncompressed size limit
                long size = entry.Size;
                if (size < 0) size = 0;
                totalUncompressedBytes += size;
                if (totalUncompressedBytes > MaxUncompressedBytes)
                {
                    return new TrainerValidationResult(false, $"Archive exceeds maximum uncompressed size limit ({MaxUncompressedBytes / (1024 * 1024)} MB).");
                }

                // Duplicate normalized paths and case-insensitive collisions
                if (!seenNormalizedPaths.Add(normalized))
                {
                    return new TrainerValidationResult(false, $"Duplicate normalized path or case collision detected: '{normalized}'.");
                }

                fileEntries.Add((rawKey, normalized, size));
            }
        }

        if (fileEntries.Count == 0)
        {
            return new TrainerValidationResult(false, "Archive contains no files.");
        }

        // Find candidate Liberty's Legacy.asi entries
        var asiEntries = fileEntries
            .Where(f => Path.GetFileName(f.NormalizedKey).Equals("Liberty's Legacy.asi", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (asiEntries.Count == 0)
        {
            return new TrainerValidationResult(false, "Missing required 'Liberty's Legacy.asi' in archive.");
        }

        if (asiEntries.Count > 1)
        {
            return new TrainerValidationResult(false, "Archive contains multiple 'Liberty's Legacy.asi' candidates.");
        }

        // Check for other .asi files (reject ambiguous archives with unrelated/multiple ASIs)
        var allAsiFiles = fileEntries
            .Where(f => f.NormalizedKey.EndsWith(".asi", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (allAsiFiles.Count > 1)
        {
            var otherAsis = allAsiFiles
                .Where(f => !Path.GetFileName(f.NormalizedKey).Equals("Liberty's Legacy.asi", StringComparison.OrdinalIgnoreCase))
                .Select(f => Path.GetFileName(f.NormalizedKey));
            return new TrainerValidationResult(false, $"Archive contains unexpected multiple ASI plugins: {string.Join(", ", otherAsis)}.");
        }

        var candidateAsi = asiEntries[0];
        string asiDirectory = Path.GetDirectoryName(candidateAsi.NormalizedKey)?.Replace('\\', '/') ?? "";
        string rootPrefix = string.IsNullOrEmpty(asiDirectory) ? "" : asiDirectory + "/";

        // Check wrapper directory depth - allow at most 1 wrapper directory
        if (!string.IsNullOrEmpty(rootPrefix))
        {
            var wrapperSegments = rootPrefix.TrimEnd('/').Split('/');
            if (wrapperSegments.Length > 1)
            {
                return new TrainerValidationResult(false, $"Archive contains nested wrapper folders deeper than 1 level: '{rootPrefix}'.");
            }

            // Ensure all entries in the archive reside inside this single wrapper directory
            bool allInsideWrapper = fileEntries.All(f => f.NormalizedKey.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase));
            if (!allInsideWrapper)
            {
                return new TrainerValidationResult(false, "Ambiguous archive: files found outside the candidate trainer wrapper folder.");
            }
        }

        // Find companion directory: "<rootPrefix>Liberty's Legacy/"
        string companionPrefix = rootPrefix + "Liberty's Legacy/";
        var companionFiles = fileEntries
            .Where(f => f.NormalizedKey.StartsWith(companionPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.NormalizedKey)
            .ToList();

        if (companionFiles.Count == 0)
        {
            return new TrainerValidationResult(false, "Missing required 'Liberty's Legacy' companion directory or it is empty.");
        }

        // Attempt version detection from wrapper folder name or companion files
        string? detectedVersion = null;
        if (!string.IsNullOrEmpty(rootPrefix))
        {
            string wrapperName = rootPrefix.TrimEnd('/');
            var match = VersionRegex.Match(wrapperName);
            if (match.Success)
            {
                detectedVersion = match.Groups[1].Value;
            }
        }

        return new TrainerValidationResult(
            IsValid: true,
            ErrorMessage: null,
            ResolvedRootPrefix: rootPrefix,
            TrainerAsiEntryKey: candidateAsi.NormalizedKey,
            CompanionEntryKeys: companionFiles,
            DetectedVersion: detectedVersion,
            TotalUncompressedBytes: totalUncompressedBytes,
            TotalEntries: fileEntries.Count
        );
    }
}
