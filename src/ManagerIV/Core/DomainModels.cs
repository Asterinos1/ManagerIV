namespace ManagerIV.Core;

/// <summary>
/// Represents a file within a mod package.
/// </summary>
/// <param name="RelativePath">The relative path of the file from the mod's root folder.</param>
/// <param name="SizeBytes">The size of the file in bytes.</param>
/// <param name="Checksum">The cryptographic checksum of the file (e.g. SHA-256) for integrity verification, if available.</param>
public record ModFile(
    string RelativePath,
    long SizeBytes,
    string? Checksum
);

/// <summary>
/// Represents a mod that has been imported into the local library and is ready for staging/deployment.
/// </summary>
/// <param name="Id">Unique identifier of the mod.</param>
/// <param name="Name">Normalized name of the mod.</param>
/// <param name="Version">Version string parsed from filename or metadata.</param>
/// <param name="Description">User-facing description of the mod.</param>
/// <param name="LibraryPath">The absolute path where the mod files are stored in the application's local library.</param>
/// <param name="Files">The collection of files belonging to this mod.</param>
/// <param name="IsEnabled">Whether this mod is currently enabled in the active profile.</param>
/// <param name="Compatibility">Derived compatibility state (e.g., "CE-compatible", "Legacy", "Mixed", "Unspecified").</param>
public record StagedMod(
    string Id,
    string Name,
    string Version,
    string Description,
    string LibraryPath,
    IReadOnlyList<ModFile> Files,
    bool IsEnabled,
    string Compatibility,
    string DisplayName = "",
    IReadOnlyList<string>? Tags = null
);

/// <summary>
/// Represents an entry in the load order.
/// </summary>
/// <param name="ModId">The unique ID of the mod.</param>
/// <param name="Target">The deployment target (Update, Plugins, Scripts).</param>
/// <param name="Priority">The load priority/order value.</param>
public record LoadOrderEntry(
    string ModId,
    DeployTarget Target,
    int Priority
);

/// <summary>
/// Holds the collection of ordered load items.
/// </summary>
/// <param name="Entries">The list of load order entries.</param>
public record LoadOrderModel(
    IReadOnlyList<LoadOrderEntry> Entries
);

/// <summary>
/// Represents the profile of the target game version based on executable properties.
/// </summary>
/// <param name="Version">The file version of the executable (e.g., "1.2.0.43").</param>
/// <param name="ExecutableSize">The size of the executable file in bytes.</param>
/// <param name="ExecutableHash">The cryptographic hash (SHA-256) of the executable.</param>
/// <param name="IsCompleteEdition">True if the version corresponds to Complete Edition, false if legacy.</param>
public record GameVersionProfile(
    string Version,
    long ExecutableSize,
    string ExecutableHash,
    bool IsCompleteEdition
)
{
    /// <summary>
    /// Checks if a version string represents Complete Edition or Legacy.
    /// Legacy version is detected if the version string normalized starts with "1.0.4.0", "1.0.7.0", or "1.0.8.0".
    /// </summary>
    public static bool CheckIsCompleteEdition(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return true;
        }

        // Normalize version string: commas to dots, remove spaces
        string normalized = version.Replace(",", ".").Replace(" ", "").Trim();

        // Legacy check
        if (normalized.StartsWith("1.0.4.0") ||
            normalized.StartsWith("1.0.7.0") ||
            normalized.StartsWith("1.0.8.0"))
        {
            return false;
        }

        return true;
    }
}


/// <summary>
/// Represents detailed conflict information for a specific virtual file target path.
/// </summary>
/// <param name="TargetPath">The virtual target path inside the game directory.</param>
/// <param name="WinnerModId">The mod ID that wins the conflict based on load order priority.</param>
/// <param name="ConflictingModIds">The mod IDs that also touch this path but lose.</param>
public record ConflictInfo(
    string TargetPath,
    string WinnerModId,
    IReadOnlyList<string> ConflictingModIds
);

/// <summary>
/// Represents the overall conflict status inside a profile.
/// </summary>
/// <param name="Conflicts">Map of virtual target paths to conflict details.</param>
/// <param name="Warnings">Warnings generated for manual merge candidates (e.g. handling.dat, .img).</param>
public record ConflictState(
    IReadOnlyDictionary<string, ConflictInfo> Conflicts,
    IReadOnlyList<string> Warnings
);

/// <summary>
/// Represents a named, independent mod configuration profile.
/// </summary>
/// <param name="Id">Unique identifier for the profile.</param>
/// <param name="Name">Name of the profile.</param>
/// <param name="GamePath">Path to the game directory.</param>
/// <param name="LibraryPath">Path to the mods library directory.</param>
/// <param name="EnabledModIds">IDs of the mods enabled in this profile.</param>
/// <param name="LoadOrder">Unified load order model.</param>
/// <param name="ConflictState">Conflict detection state for this profile.</param>
/// <param name="LastKnownVersion">Cached version profile of the game executable.</param>
/// <param name="GpuVramMb">The user's GPU VRAM limit in Megabytes.</param>
public record Profile(
    string Id,
    string Name,
    string GamePath,
    string LibraryPath,
    IReadOnlyList<string> EnabledModIds,
    LoadOrderModel LoadOrder,
    ConflictState ConflictState,
    GameVersionProfile? LastKnownVersion = null,
    int GpuVramMb = 2048,
    IReadOnlyDictionary<string, string>? InstalledToolVersions = null
)
{
    public IReadOnlyDictionary<string, string> ToolVersions => InstalledToolVersions ?? new Dictionary<string, string>();
}

/// <summary>
/// Records a single file deployed by a backend tool.
/// </summary>
public record InstalledToolFile(
    string SourceTool,
    string InstalledPath,
    string Sha256
);

/// <summary>
/// Represents a GTA IV save game profile directory state.
/// </summary>
/// <param name="FolderName">The physical folder name, e.g., "975EF3C9" or "975EF3C9_LCPDFR".</param>
/// <param name="DisplayName">The user-friendly display name.</param>
/// <param name="IsActive">Whether this save profile is currently the active one loaded by the game.</param>
/// <param name="FullPath">The absolute physical path to the save profile directory.</param>
/// <param name="SaveFileCount">Count of SGTA40* save files in the folder.</param>
/// <param name="LastModified">Most recent save file timestamp.</param>
/// <param name="TotalSizeBytes">Total byte size of all save files in the directory.</param>
public record SaveProfile(
    string FolderName,
    string DisplayName,
    bool IsActive,
    string FullPath,
    int SaveFileCount = 0,
    DateTime? LastModified = null,
    long TotalSizeBytes = 0
)
{
    public string SizeDisplay => TotalSizeBytes switch
    {
        <= 0 => "0 KB",
        < 1024 * 1024 => $"{TotalSizeBytes / 1024.0:F1} KB",
        _ => $"{TotalSizeBytes / (1024.0 * 1024.0):F2} MB"
    };

    public string LastModifiedDisplay => LastModified.HasValue
        ? LastModified.Value.ToString("g")
        : "Unknown";
};
