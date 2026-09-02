namespace ManagerIV.Core;

/// <summary>
/// Configuration model for a backend tool release.
/// </summary>
public record ToolConfig(
    string Id,
    string Name,
    string? GitHub,
    string? DirectUrl,
    string? License,
    IReadOnlyList<string>? SupportedVersions,
    string InstallTo
);

/// <summary>
/// Configuration model for a specific game version adapter layout.
/// </summary>
public record AdapterConfig(
    string UpdateFolder,
    string AsiFolder,
    string ScriptFolder,
    string PriorityMechanism
);

/// <summary>
/// Configuration model for filesystem paths.
/// </summary>
public record PathsConfig(
    string GameDir,
    string LibraryDir
);

/// <summary>
/// Root model representing the application manifest configuration.
/// </summary>
public record ManifestConfig(
    IReadOnlyList<ToolConfig> Tools,
    IReadOnlyDictionary<string, AdapterConfig> Adapters,
    PathsConfig Paths
);
