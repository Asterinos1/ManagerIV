using System.IO;

namespace ManagerIV.Core;

/// <summary>
/// Validates Liberty's Legacy package structure, paths, and security constraints.
/// </summary>
public interface ILibertyTrainerValidator
{
    /// <summary>
    /// Validates an archive file on disk.
    /// </summary>
    TrainerValidationResult ValidateArchive(string archivePath);

    /// <summary>
    /// Validates an archive from a readable stream.
    /// </summary>
    TrainerValidationResult ValidateArchiveStream(Stream stream);
}
