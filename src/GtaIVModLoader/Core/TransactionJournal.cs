using System.Collections.Generic;

namespace GtaIVModLoader.Core;

/// <summary>
/// Specifies the type of operation recorded in the transaction journal.
/// </summary>
public enum JournalOpType
{
    CreateJunction,
    CreateHardLink,
    CreateSymbolicLink,
    BackupAndReplaceFile,
    DeleteFile,
    DeleteDirectory
}

/// <summary>
/// Represents a single recorded filesystem operation inside the transaction journal.
/// </summary>
/// <param name="Type">The type of operation performed.</param>
/// <param name="Path">The file or directory path that was mutated.</param>
/// <param name="Target">The link target path, if applicable.</param>
/// <param name="BackupPath">The path to the backed-up file, if applicable.</param>
/// <param name="IsDirectory">True if the target is a directory rather than a file.</param>
public record JournalEntry(
    JournalOpType Type,
    string Path,
    string? Target,
    string? BackupPath = null,
    bool IsDirectory = false
);

/// <summary>
/// Records intended filesystem operations sequentially, providing a source of truth for safe rollback.
/// </summary>
public class TransactionJournal
{
    private readonly List<JournalEntry> _entries = new();

    /// <summary>
    /// Gets the list of operations recorded in the journal.
    /// </summary>
    public IReadOnlyList<JournalEntry> Entries => _entries;

    /// <summary>
    /// Records a new filesystem operation to the journal.
    /// </summary>
    public void Record(JournalEntry entry)
    {
        _entries.Add(entry);
    }

    /// <summary>
    /// Clears all recorded operations from the journal.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
    }
}
