using System;
using System.IO;

namespace ManagerIV.Core;

/// <summary>
/// Service responsible for managing backup state and executing rollbacks via TransactionJournal.
/// </summary>
public class BackupRollbackService
{
    private readonly IFileSystemLinker _linker;
    private readonly string _backupDir;

    public BackupRollbackService(IFileSystemLinker linker, string backupDir)
    {
        _linker = linker ?? throw new ArgumentNullException(nameof(linker));
        _backupDir = backupDir ?? throw new ArgumentNullException(nameof(backupDir));
    }

    /// <summary>
    /// Executes a rollback of all operations recorded in the journal in reverse order.
    /// </summary>
    public void Rollback(TransactionJournal journal)
    {
        var entries = journal.Entries;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            try
            {
                RollbackEntry(entry);
            }
            catch (Exception ex)
            {
                // In production, this should be written to the app logger.
                Console.WriteLine($"[ROLLBACK ERROR] Failed to roll back entry {entry}: {ex.Message}");
            }
        }
        journal.Clear();
    }

    private void RollbackEntry(JournalEntry entry)
    {
        switch (entry.Type)
        {
            case JournalOpType.CreateJunction:
                if (_linker.IsJunction(entry.Path))
                {
                    _linker.DeleteDirectory(entry.Path);
                }
                break;

            case JournalOpType.CreateHardLink:
                if (_linker.FileExists(entry.Path))
                {
                    _linker.DeleteFile(entry.Path);
                }
                break;

            case JournalOpType.CreateSymbolicLink:
                if (entry.IsDirectory)
                {
                    if (_linker.IsSymbolicLink(entry.Path) || _linker.DirectoryExists(entry.Path))
                    {
                        _linker.DeleteDirectory(entry.Path);
                    }
                }
                else
                {
                    if (_linker.IsSymbolicLink(entry.Path) || _linker.FileExists(entry.Path))
                    {
                        _linker.DeleteFile(entry.Path);
                    }
                }
                break;

            case JournalOpType.BackupAndReplaceFile:
                if (entry.BackupPath != null && File.Exists(entry.BackupPath))
                {
                    string? dir = Path.GetDirectoryName(entry.Path);
                    if (dir != null && !_linker.DirectoryExists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Copy(entry.BackupPath, entry.Path, overwrite: true);
                    File.Delete(entry.BackupPath);
                }
                else if (_linker.FileExists(entry.Path))
                {
                    _linker.DeleteFile(entry.Path);
                }
                break;

            case JournalOpType.DeleteFile:
                if (entry.BackupPath != null && File.Exists(entry.BackupPath))
                {
                    string? dir = Path.GetDirectoryName(entry.Path);
                    if (dir != null && !_linker.DirectoryExists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Copy(entry.BackupPath, entry.Path, overwrite: true);
                    File.Delete(entry.BackupPath);
                }
                break;

            case JournalOpType.DeleteDirectory:
                if (!_linker.DirectoryExists(entry.Path))
                {
                    Directory.CreateDirectory(entry.Path);
                }
                break;
        }
    }

    /// <summary>
    /// Safely performs a junction creation and records it to the journal.
    /// </summary>
    public void ExecuteCreateJunction(TransactionJournal journal, string junctionPath, string targetPath)
    {
        journal.Record(new JournalEntry(JournalOpType.CreateJunction, junctionPath, targetPath, IsDirectory: true));
        _linker.CreateJunction(junctionPath, targetPath);
    }

    /// <summary>
    /// Safely performs a hardlink creation and records it to the journal.
    /// </summary>
    public void ExecuteCreateHardLink(TransactionJournal journal, string linkPath, string targetPath)
    {
        journal.Record(new JournalEntry(JournalOpType.CreateHardLink, linkPath, targetPath));
        _linker.CreateHardLink(linkPath, targetPath);
    }

    /// <summary>
    /// Safely performs a symlink creation and records it to the journal.
    /// </summary>
    public void ExecuteCreateSymbolicLink(TransactionJournal journal, string linkPath, string targetPath, bool isDirectory)
    {
        journal.Record(new JournalEntry(JournalOpType.CreateSymbolicLink, linkPath, targetPath, IsDirectory: isDirectory));
        _linker.CreateSymbolicLink(linkPath, targetPath, isDirectory);
    }

    /// <summary>
    /// Safely replaces a file after backing it up, and records it to the journal.
    /// </summary>
    public void ExecuteBackupAndReplaceFile(TransactionJournal journal, string filePath, string sourcePath)
    {
        string? backupFilePath = null;
        if (_linker.FileExists(filePath))
        {
            Directory.CreateDirectory(_backupDir);
            backupFilePath = Path.Combine(_backupDir, Guid.NewGuid().ToString() + ".bak");
            File.Copy(filePath, backupFilePath, overwrite: true);
        }

        journal.Record(new JournalEntry(JournalOpType.BackupAndReplaceFile, filePath, sourcePath, backupFilePath));

        string? dir = Path.GetDirectoryName(filePath);
        if (dir != null && !_linker.DirectoryExists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.Copy(sourcePath, filePath, overwrite: true);
    }
}
