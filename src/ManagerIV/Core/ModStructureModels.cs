using System.Collections.Generic;

namespace ManagerIV.Core;

public enum DeploymentTarget
{
    UpdateFolder,
    PluginsFolder,
    ScriptsFolder,
    Unknown
}

public enum VersionCompatibility
{
    CompleteEditionOnly,
    LegacyOnly,
    Both,
    Unknown
}

public record DetectedFileGroup(
    DeploymentTarget Target,
    string SourcePathPrefix,   // path inside archive to strip when staging
    IReadOnlyList<string> Entries
);

public record DependencyHint(string Name, bool IsInstalled);

public record ModStructureReport(
    IReadOnlyList<DetectedFileGroup> DetectedTargets,
    VersionCompatibility VersionCompatibility,
    IReadOnlyList<DependencyHint> Dependencies,
    IReadOnlyList<string> ConflictHints,
    IReadOnlyList<string> UnresolvedFiles,
    string? RawReadmeText,
    bool IsDualTarget   // true when both CE and legacy structures coexist
);

public record ModFileNameParseResult(
    string DisplayName,   // Clean human-readable name, title-cased, spaces normalized
    string? Version,      // Detected version string e.g. "1.1", "2.0", "8.1", null if none found
    IReadOnlyList<string> Tags  // Trailing qualifiers e.g. ["Stable"], ["HD", "Fusion Fix"], ["manual"]
);

