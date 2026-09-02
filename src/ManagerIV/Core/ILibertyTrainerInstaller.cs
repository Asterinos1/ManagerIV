namespace ManagerIV.Core;

/// <summary>
/// Manages deployment, conflict detection, updates, and uninstallation for Liberty's Legacy Trainer.
/// </summary>
public interface ILibertyTrainerInstaller
{
    /// <summary>
    /// Gets the current installation status of Liberty's Legacy in the game directory.
    /// </summary>
    TrainerStatus GetTrainerStatus(string gamePath);

    /// <summary>
    /// Gets the detected or recorded version string for Liberty's Legacy.
    /// </summary>
    string GetInstalledVersion(string gamePath, Profile? profile);

    /// <summary>
    /// Detects other potential trainer plugins that may conflict with Liberty's Legacy.
    /// </summary>
    TrainerConflictInfo DetectConflicts(string gamePath);

    /// <summary>
    /// Deploys trainer files from a validated archive to the game directory with rollback support.
    /// </summary>
    /// <param name="archivePath">Path to validated ZIP file.</param>
    /// <param name="gamePath">Target GTA IV game directory.</param>
    /// <param name="profile">Active mod profile.</param>
    /// <param name="manifest">Tracked installed tools manifest.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <returns>Detected or assigned version string.</returns>
    Task<string> InstallTrainerAsync(
        string archivePath,
        string gamePath,
        Profile profile,
        List<InstalledToolFile> manifest,
        IProgress<string>? progress = null
    );

    /// <summary>
    /// Uninstalls Liberty's Legacy files from the game directory.
    /// </summary>
    /// <param name="gamePath">Target GTA IV game directory.</param>
    /// <param name="manifest">Tracked installed tools manifest.</param>
    /// <param name="preserveUserData">Whether to preserve user-modified settings and generated data.</param>
    Task UninstallTrainerAsync(
        string gamePath,
        List<InstalledToolFile> manifest,
        bool preserveUserData = true
    );
}
