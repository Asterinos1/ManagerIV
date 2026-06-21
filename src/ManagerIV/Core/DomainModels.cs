using System;
using System.Collections.Generic;

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
    string Compatibility
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
);

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
public record Profile(
    string Id,
    string Name,
    string GamePath,
    string LibraryPath,
    IReadOnlyList<string> EnabledModIds,
    LoadOrderModel LoadOrder,
    ConflictState ConflictState,
    GameVersionProfile? LastKnownVersion = null
);
