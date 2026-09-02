namespace ManagerIV.Core;

/// <summary>
/// Monitors the user's Downloads directory for candidate trainer archives.
/// </summary>
public interface ILibertyTrainerDownloadMonitor
{
    /// <summary>
    /// Gets the resolved absolute path to the user's standard Downloads folder.
    /// </summary>
    string GetDownloadsDirectory();

    /// <summary>
    /// Monitors the downloads directory for a completed, valid Liberty's Legacy ZIP archive.
    /// </summary>
    /// <param name="downloadsDir">Directory path to watch.</param>
    /// <param name="startTime">Timestamp marking the start of the user workflow.</param>
    /// <param name="cancellationToken">Cancellation token to terminate monitoring.</param>
    /// <param name="progress">Optional progress reporter for status updates.</param>
    /// <returns>Absolute path to the validated ZIP file, or null if cancelled.</returns>
    Task<string?> WaitForCandidateArchiveAsync(
        string downloadsDir,
        DateTime startTime,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null
    );
}
