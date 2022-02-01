namespace Ipfs.VirtualDisk;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal class PredefinedFile
{
    static PredefinedFile()
    {
        const string prefix = "Ipfs.VirtualDisk.Resources.PredefineFiles.";
        All = Assembly.GetExecutingAssembly()
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix))
            .Select(name => new PredefinedFile
            {
                Name = string.Concat(@"\", name.AsSpan(prefix.Length)),
                Data = GetData(name)
            })
            .ToDictionary(f => f.Name, f => f);
    }

    public static Dictionary<string, PredefinedFile> All { get; private set; }
    public byte[] Data { get; set; }
    public string Name { get; set; }

    private static byte[] GetData(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
