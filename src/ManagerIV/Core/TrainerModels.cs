namespace ManagerIV.Core;

/// <summary>
/// Status of Liberty's Legacy Trainer in the game directory.
/// </summary>
public enum TrainerStatus
{
    Missing,
    Installed,
    RepairNeeded
}

/// <summary>
/// Result of Liberty's Legacy ZIP archive structural validation.
/// </summary>
public record TrainerValidationResult(
    bool IsValid,
    string? ErrorMessage = null,
    string? ResolvedRootPrefix = null,
    string? TrainerAsiEntryKey = null,
    IReadOnlyList<string>? CompanionEntryKeys = null,
    string? DetectedVersion = null,
    long TotalUncompressedBytes = 0,
    int TotalEntries = 0
);

/// <summary>
/// Conflict detection summary for trainer plugins.
/// </summary>
public record TrainerConflictInfo(
    bool HasConflicts,
    IReadOnlyList<string> ConflictingFiles
);
