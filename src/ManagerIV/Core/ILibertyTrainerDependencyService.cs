namespace ManagerIV.Core;

/// <summary>
/// Verifies and installs runtime dependencies required by Liberty's Legacy on Complete Edition.
/// </summary>
public interface ILibertyTrainerDependencyService
{
    /// <summary>
    /// Checks whether the target game profile is compatible with Complete Edition requirements.
    /// </summary>
    bool CheckIsCompleteEdition(Profile? profile);

    /// <summary>
    /// Ensures UAL, ScriptHook.dll, and aCompleteEditionHook.asi are present in the game directory.
    /// </summary>
    /// <param name="gamePath">The target GTA IV game directory.</param>
    /// <param name="profile">The active mod profile.</param>
    /// <param name="manifest">The tracked installed tools manifest.</param>
    /// <param name="installStandaloneUalCallback">Optional fallback callback to install standalone UAL.</param>
    /// <returns>List of newly installed dependency files.</returns>
    Task<List<InstalledToolFile>> EnsureDependenciesAsync(
        string gamePath,
        Profile profile,
        List<InstalledToolFile> manifest,
        Func<Task>? installStandaloneUalCallback = null
    );
}
