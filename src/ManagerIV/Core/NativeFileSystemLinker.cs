using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ManagerIV.Core;

/// <summary>
/// Native implementation of IFileSystemLinker utilizing Windows P/Invoke for hard links and directory junctions.
/// </summary>
public class NativeFileSystemLinker : IFileSystemLinker
{
    private readonly bool _useSymlinkFallback;

    // P/Invoke constants
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint FILE_SHARE_DELETE = 0x00000004;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
    private const uint FSCTL_SET_REPARSE_POINT = 0x000900A4;
    private const uint FSCTL_GET_REPARSE_POINT = 0x000900A8;
    private const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;
    private const uint IO_REPARSE_TAG_SYMLINK = 0xA000000C;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile
    );

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeHandle hDevice,
        uint dwIoControlCode,
        byte[]? lpInBuffer,
        uint nInBufferSize,
        byte[]? lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped
    );

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(
        string lpFileName,
        string lpExistingFileName,
        IntPtr lpSecurityAttributes
    );

    public NativeFileSystemLinker(bool useSymlinkFallback = false)
    {
        _useSymlinkFallback = useSymlinkFallback;
    }

    public void CreateHardLink(string linkPath, string targetPath)
    {
        string absoluteTarget = Path.GetFullPath(targetPath);
        string absoluteLink = Path.GetFullPath(linkPath);

        // Ensure target exists
        if (!File.Exists(absoluteTarget))
        {
            throw new FileNotFoundException("Target file not found for hard link.", absoluteTarget);
        }

        // Ensure destination directory exists
        string? linkDir = Path.GetDirectoryName(absoluteLink);
        if (linkDir != null && !Directory.Exists(linkDir))
        {
            Directory.CreateDirectory(linkDir);
        }

        // Overwrite by deleting existing file first
        if (File.Exists(absoluteLink))
        {
            File.Delete(absoluteLink);
        }

        bool success = CreateHardLinkW(absoluteLink, absoluteTarget, IntPtr.Zero);
        if (!success)
        {
            int err = Marshal.GetLastWin32Error();
            // Fallback to copy if on non-NTFS or across volumes (ERROR_NOT_SAME_DEVICE = 17)
            if (err == 17 || err == 1 || err == 50) // 17 = Not same device, 1 = Invalid function, 50 = Not supported
            {
                File.Copy(absoluteTarget, absoluteLink, overwrite: true);
            }
            else
            {
                throw new Win32Exception(err, $"Failed to create hard link from '{absoluteLink}' to '{absoluteTarget}'.");
            }
        }
    }

    public void CreateJunction(string junctionPath, string targetPath)
    {
        if (_useSymlinkFallback)
        {
            CreateSymbolicLink(junctionPath, targetPath, isDirectory: true);
            return;
        }

        string absoluteTarget = Path.GetFullPath(targetPath);
        string absoluteJunction = Path.GetFullPath(junctionPath);

        if (!Directory.Exists(absoluteTarget))
        {
            throw new DirectoryNotFoundException($"Target directory '{absoluteTarget}' not found for junction creation.");
        }

        // Create the directory for the junction if it doesn't exist
        if (!Directory.Exists(absoluteJunction))
        {
            Directory.CreateDirectory(absoluteJunction);
        }

        // Prepare the reparse point buffer
        string substituteName = @"\??\" + absoluteTarget;
        string printName = absoluteTarget;

        byte[] substituteBytes = Encoding.Unicode.GetBytes(substituteName);
        byte[] printBytes = Encoding.Unicode.GetBytes(printName);

        ushort substituteLength = (ushort)substituteBytes.Length;
        ushort printLength = (ushort)printBytes.Length;

        ushort pathBufferLength = (ushort)(substituteLength + 2 + printLength + 2);
        ushort reparseDataLength = (ushort)(8 + pathBufferLength);
        byte[] inBuffer = new byte[8 + reparseDataLength];

        // ReparseTag (IO_REPARSE_TAG_MOUNT_POINT)
        BitConverter.GetBytes(IO_REPARSE_TAG_MOUNT_POINT).CopyTo(inBuffer, 0);
        // ReparseDataLength
        BitConverter.GetBytes(reparseDataLength).CopyTo(inBuffer, 4);
        // Reserved (0)
        BitConverter.GetBytes((ushort)0).CopyTo(inBuffer, 6);

        // SubstituteNameOffset (0)
        BitConverter.GetBytes((ushort)0).CopyTo(inBuffer, 8);
        // SubstituteNameLength
        BitConverter.GetBytes(substituteLength).CopyTo(inBuffer, 10);
        // PrintNameOffset (substituteLength + 2)
        BitConverter.GetBytes((ushort)(substituteLength + 2)).CopyTo(inBuffer, 12);
        // PrintNameLength
        BitConverter.GetBytes(printLength).CopyTo(inBuffer, 14);

        // PathBuffer contents
        substituteBytes.CopyTo(inBuffer, 16);
        // Null terminators at 16 + substituteLength and 16 + substituteLength + 1 are already 0
        printBytes.CopyTo(inBuffer, 16 + substituteLength + 2);
        // Ending null terminators are already 0

        // Open the directory handle
        using SafeFileHandle handle = CreateFileW(
            absoluteJunction,
            GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero
        );

        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open directory handle at '{absoluteJunction}'.");
        }

        // Set the reparse point
        bool success = DeviceIoControl(
            handle,
            FSCTL_SET_REPARSE_POINT,
            inBuffer,
            (uint)inBuffer.Length,
            null,
            0,
            out _,
            IntPtr.Zero
        );

        if (!success)
        {
            int err = Marshal.GetLastWin32Error();
            throw new Win32Exception(err, $"Failed to set directory junction reparse point at '{absoluteJunction}'.");
        }
    }

    public void CreateSymbolicLink(string linkPath, string targetPath, bool isDirectory)
    {
        string absoluteTarget = Path.GetFullPath(targetPath);
        string absoluteLink = Path.GetFullPath(linkPath);

        if (isDirectory)
        {
            Directory.CreateSymbolicLink(absoluteLink, absoluteTarget);
        }
        else
        {
            File.CreateSymbolicLink(absoluteLink, absoluteTarget);
        }
    }

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool IsJunction(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        try
        {
            var di = new DirectoryInfo(path);
            if (!di.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return false;
            }
            return GetReparseTag(path) == IO_REPARSE_TAG_MOUNT_POINT;
        }
        catch
        {
            return false;
        }
    }

    public bool IsSymbolicLink(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return false;
        }

        try
        {
            var fsi = File.Exists(path) ? (FileSystemInfo)new FileInfo(path) : new DirectoryInfo(path);
            if (!fsi.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return false;
            }
            return GetReparseTag(path) == IO_REPARSE_TAG_SYMLINK;
        }
        catch
        {
            return false;
        }
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void DeleteDirectory(string path)
    {
        if (IsJunction(path) || IsSymbolicLink(path))
        {
            Directory.Delete(path);
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private uint GetReparseTag(string path)
    {
        using SafeFileHandle handle = CreateFileW(
            path,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero
        );

        if (handle.IsInvalid)
        {
            return 0;
        }

        byte[] outBuffer = new byte[1024];
        bool success = DeviceIoControl(
            handle,
            FSCTL_GET_REPARSE_POINT,
            null,
            0,
            outBuffer,
            (uint)outBuffer.Length,
            out uint bytesReturned,
            IntPtr.Zero
        );

        if (!success || bytesReturned < 4)
        {
            return 0;
        }

        return BitConverter.ToUInt32(outBuffer, 0);
    }
}
