namespace Ipfs.VirtualDisk;

using DokanNet;
using Ipfs.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

/// <summary>
/// Maps Dokan opeations into IPFS.
/// </summary>
internal partial class IpfsDokan : IDokanOperations
{
    private const string rootName = @"\";
    private static readonly IpfsClient ipfs = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    private static readonly DirectorySecurity readonlyDirectorySecurity = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    private static readonly FileSecurity readonlyFileSecurity = new();

    private static readonly string[] rootFolders = { "ipfs", "ipns" };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    static IpfsDokan()
    {
        var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        var everyoneRead = new FileSystemAccessRule(
            everyone,
            FileSystemRights.ReadAndExecute | FileSystemRights.ListDirectory,
            AccessControlType.Allow);
        readonlyFileSecurity.AddAccessRule(everyoneRead);
        readonlyFileSecurity.SetOwner(everyone);
        readonlyFileSecurity.SetGroup(everyone);
        readonlyDirectorySecurity.AddAccessRule(everyoneRead);
        readonlyDirectorySecurity.SetOwner(everyone);
        readonlyDirectorySecurity.SetGroup(everyone);
    }

    public void Cleanup(string fileName, IDokanFileInfo info)
    {
        // Nothing to do.
    }

    public void CloseFile(string fileName, IDokanFileInfo info)
    {
        // Nothing to do.
    }

    public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, IDokanFileInfo info)
    {
        // Read only access.
        if (mode != FileMode.Open || (access & DokanNet.FileAccess.WriteData) != 0)
        {
            return DokanResult.AccessDenied;
        }

        // Root and root folders are always present.
        if (fileName == rootName || rootFolders.Any(name => fileName == (rootName + name)))
        {
            info.IsDirectory = true;
            return DokanResult.Success;
        }

        // Predefined files
        if (PredefinedFile.All.TryGetValue(fileName, out var predefinedFile))
        {
            info.Context = predefinedFile;
            return DokanResult.Success;
        }

        // Get file info from IPFS
        string IFileSystemNodeName = fileName.Replace(@"\", "/");
        try
        {
            var file = GetIFileSystemNode(IFileSystemNodeName);
            info.Context = file;
            info.IsDirectory = file.IsDirectory;
        }
        catch
        {
            return DokanResult.FileNotFound;
        }

        return DokanResult.Success;
    }

    public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info) => DokanResult.AccessDenied;

    public NtStatus DeleteFile(string fileName, IDokanFileInfo info) => DokanResult.AccessDenied;

    public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
    {
        if (info.Context is IFileSystemNode file)
        {
            files = file.Links
                .Select(
                    link =>
                        new FileInformation()
                        {
                            FileName = link.Name,
                            Length = link.Size,
                            Attributes = FileAttributes.ReadOnly | (file.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal)
                        })
                .ToList();
            return DokanResult.Success;
        }

        // '/ipfs' contains the pinned files.
        if (fileName == @"\ipfs")
        {
            var cids = ipfs.Pin.ListAsync().GetAwaiter().GetResult();
            files = cids
                .Select(
                    cid =>
                    {
                        try
                        {
                            return GetIFileSystemNode(cid);
                        }
                        catch
                        {
                            return null;
                        }
                    })
                .Where(f => f is not null)
                .Select(
                    pinnedFile =>
                        new FileInformation
                        {
                            FileName = pinnedFile.Id,
                            Length = pinnedFile.Size,
                            Attributes = FileAttributes.ReadOnly | (pinnedFile.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal)
                        })
                .ToList();
            return DokanResult.Success;
        }

        // The root consists of the root folders and the predefined files.
        if (fileName == rootName)
        {
            files = rootFolders
                .Select(
                    name =>
                        new FileInformation
                        {
                            FileName = name,
                            Attributes = FileAttributes.Directory | FileAttributes.ReadOnly,
                            LastAccessTime = DateTime.Now
                        })
                .ToList();

            foreach (var predefinedFile in PredefinedFile.All.Values)
            {
                files.Add(
                    new FileInformation
                    {
                        FileName = Path.GetFileName(predefinedFile.Name),
                        Attributes = FileAttributes.ReadOnly,
                        Length = predefinedFile.Data.Length
                    });
            }

            return DokanResult.Success;
        }

        // Can not determine the contents of the root folders.
        if (rootFolders.Any(name => fileName == (rootName + name)))
        {
            files = Array.Empty<FileInformation>();
            return DokanResult.Success;
        }

        files = Array.Empty<FileInformation>();
        return DokanResult.Success;
    }

    public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
    {
        files = Array.Empty<FileInformation>();
        return DokanResult.NotImplemented;
    }

    public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
    {
        streams = null;
        return DokanResult.NotImplemented;
    }

    public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info) => DokanResult.Success;

    public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
    {
        freeBytesAvailable = 0;
        totalNumberOfBytes = 0;
        totalNumberOfFreeBytes = 0;

        return NtStatus.Success;
    }

    public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
    {
        fileInfo = new FileInformation
        {
            FileName = fileName,
            Attributes = FileAttributes.ReadOnly
        };
        if (info.Context is IFileSystemNode file)
        {
            if (file.IsDirectory)
            {
                fileInfo.Attributes |= FileAttributes.Directory;
            }

            fileInfo.Length = file.Size;

            return DokanResult.Success;
        }

        if (info.Context is PredefinedFile predefinedFile)
        {
            fileInfo.Length = predefinedFile.Data.Length;
            return DokanResult.Success;
        }

        // Root info
        if (fileName == rootName)
        {
            fileInfo.Attributes |= FileAttributes.Directory;
            fileInfo.LastAccessTime = DateTime.Now;

            return DokanResult.Success;
        }

        // Root folder info
        if (rootFolders.Any(name => fileName == (rootName + name)))
        {
            fileInfo.Attributes |= FileAttributes.Directory;
            fileInfo.LastAccessTime = DateTime.Now;

            return DokanResult.Success;
        }

        return DokanResult.FileNotFound;
    }

    public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
    {
        security = info.IsDirectory ? readonlyDirectorySecurity : readonlyFileSecurity;
        return DokanResult.Success;
    }

    public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
    {
        volumeLabel = "Interplanetary";
        features = FileSystemFeatures.ReadOnlyVolume
            | FileSystemFeatures.CasePreservedNames
            | FileSystemFeatures.CaseSensitiveSearch
            | FileSystemFeatures.PersistentAcls
            | FileSystemFeatures.SupportsRemoteStorage
            | FileSystemFeatures.UnicodeOnDisk
            | FileSystemFeatures.SupportsObjectIDs;
        fileSystemName = "IPFS";
        info.IsDirectory = true;
        maximumComponentLength = 255;

        return NtStatus.Success;
    }

    public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info) => DokanResult.Success;

    public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
    {
        Console.WriteLine("IPFS mounted");
        return NtStatus.Success;
    }

    public NtStatus Mounted(IDokanFileInfo info)
    {
        Console.WriteLine("IPFS mounted");
        return NtStatus.Success;
    }

    public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info) => DokanResult.AccessDenied;

    public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
    {
        if (info.Context is PredefinedFile predefinedFile)
        {
            bytesRead = (int)Math.Min(buffer.LongLength, predefinedFile.Data.LongLength - offset);
            Buffer.BlockCopy(predefinedFile.Data, (int)offset, buffer, 0, bytesRead);
            return DokanResult.Success;
        }

        var file = (IFileSystemNode)info.Context;

        using var data = ipfs.FileSystem.ReadFileAsync(file.Id, offset, buffer.LongLength).GetAwaiter().GetResult();

        // Fill the entire buffer
        bytesRead = 0;
        int bufferOffset = 0;
        int remainingBytes = buffer.Length;
        while (remainingBytes > 0)
        {
            int n = data.Read(buffer, bufferOffset, remainingBytes);
            if (n < 1)
            {
                break;
            }

            bufferOffset += n;
            remainingBytes -= n;
            bytesRead += n;
        }

        return DokanResult.Success;
    }

    public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info) => DokanResult.AccessDenied;

    public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info) => DokanResult.AccessDenied;

    public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info) => DokanResult.AccessDenied;

    public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info) => DokanResult.AccessDenied;

    public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info) => DokanResult.AccessDenied;

    public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info) => DokanResult.Success;

    public NtStatus Unmounted(IDokanFileInfo info)
    {
        Console.WriteLine("IPFS unmounted");
        return NtStatus.Success;
    }

    public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
    {
        bytesWritten = 0;
        return DokanResult.AccessDenied;
    }

    private static IFileSystemNode GetIFileSystemNode(string name) => ipfs.FileSystem.ListFileAsync(name).GetAwaiter().GetResult();
}
