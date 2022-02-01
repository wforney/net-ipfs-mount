namespace Ipfs.VirtualDisk.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading;

[TestClass]
public class RunnerTest
{
    [AssemblyInitialize]
    public static void Mount(TestContext context)
    {
        var vdisk = new Runner();
        var thread = new Thread(async () => await Runner.Mount("t:", null, true));
        thread.Start();

        // Wait for Mount to work.
        while (true)
        {
            Thread.Sleep(0);
            var info = new DriveInfo("t");
            if (info.IsReady)
            {
                break;
            }
        }
    }

    [AssemblyCleanup]
    public static void Unmount()
    {
        _ = new Runner();
        Runner.Unmount("t:");
    }
}
