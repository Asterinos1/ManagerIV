using System.Collections.Generic;

namespace ManagerIV.Core;

/// <summary>
/// Specifies the classification of a deployment folder location.
/// </summary>
public enum DeploymentTarget
{
    /// <summary>Deployed in the game's update folder.</summary>
    UpdateFolder,
    /// <summary>Deployed in the plugins folder.</summary>
    PluginsFolder,
    /// <summary>Deployed in the scripts folder.</summary>
    ScriptsFolder,
    /// <summary>Deployment location is unknown.</summary>
    Unknown
}

/// <summary>
/// Specifies the target game version compatibility for a mod.
/// </summary>
public enum VersionCompatibility
{
    /// <summary>Compatible only with Complete Edition.</summary>
    CompleteEditionOnly,
    /// <summary>Compatible only with legacy version (e.g. 1.0.7.0/1.0.8.0).</summary>
    LegacyOnly,
    /// <summary>Compatible with both game versions.</summary>
    Both,
    /// <summary>Compatibility is unknown.</summary>
    Unknown
}

/// <summary>
/// Describes a collection of files inside the archive detected to belong to a specific target folder.
/// </summary>
/// <param name="Target">The destination target folder.</param>
/// <param name="SourcePathPrefix">The path prefix inside the archive to strip when deploying.</param>
/// <param name="Entries">The collection of files inside the group.</param>
public record DetectedFileGroup(
    DeploymentTarget Target,
    string SourcePathPrefix,
    IReadOnlyList<string> Entries
);

/// <summary>
/// Represents a dependency hint discovered in the mod's readme/instructions.
/// </summary>
/// <param name="Name">The name of the dependency.</param>
/// <param name="IsInstalled">Whether the dependency is currently installed.</param>
public record DependencyHint(string Name, bool IsInstalled);

/// <summary>
/// Holds analysis reports on a mod's structure, files, and metadata.
/// </summary>
/// <param name="DetectedTargets">The list of detected target files grouped by destination.</param>
/// <param name="VersionCompatibility">The derived version compatibility classification.</param>
/// <param name="Dependencies">A list of dependency hints referenced in the mod.</param>
/// <param name="ConflictHints">Hints pointing to potential incompatibility warnings.</param>
/// <param name="UnresolvedFiles">Files whose destination could not be auto-resolved.</param>
/// <param name="RawReadmeText">Raw readme text content if found.</param>
/// <param name="IsDualTarget">True if both legacy and Complete Edition folders were discovered.</param>
public record ModStructureReport(
    IReadOnlyList<DetectedFileGroup> DetectedTargets,
    VersionCompatibility VersionCompatibility,
    IReadOnlyList<DependencyHint> Dependencies,
    IReadOnlyList<string> ConflictHints,
    IReadOnlyList<string> UnresolvedFiles,
    string? RawReadmeText,
    bool IsDualTarget
);

/// <summary>
/// Details parsed from a mod archive's filename.
/// </summary>
/// <param name="DisplayName">Cleaned up, human-readable display name.</param>
/// <param name="Version">The parsed version string, if any.</param>
/// <param name="Tags">Additional tags/qualifiers detected from the file name.</param>
public record ModFileNameParseResult(
    string DisplayName,
    string? Version,
    IReadOnlyList<string> Tags
);

