namespace Ipfs.VirtualDisk;

using Common.Logging;
using DokanNet;
using Ipfs.Api;
using NDesk.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Command line parsing
        bool help = false;
        bool debugging = false;
        bool unmount = false;
        string apiUrl = "http://127.0.0.1:5001";
        var p = new OptionSet() {
                { "s|server=", "IPFS API server address", v => apiUrl = v},
                { "u|unmount", "Unmount the drive", v => unmount = v != null },
                { "d|debug", "Display debugging information", v => debugging = v != null },
                { "h|?|help", "Show this help", v => help = v != null },
            };
        List<string> extras;
        try
        {
            extras = p.Parse(args);
        }
        catch (OptionException e)
        {
            return ShowError(e.Message);
        }

        if (help)
        {
            return ShowHelp(p);
        }

        // Logging
        var log = LogManager.GetLogger("ipfs-mount");

        // Allow colon after drive letter
        if (extras.Count < 1)
        {
            return ShowError("Missing the drive letter.");
        }
        else if (extras.Count > 1)
        {
            return ShowError("Unknown option");
        }

        string drive = extras[0];
        if (drive.EndsWith(":"))
        {
            drive = drive[0..^1];
        }

        drive += @":\";

        // Do the command
        var program = new Runner();
        try
        {
            if (unmount)
            {
                Runner.Unmount(drive);
            }
            else
            {
                await Runner.Mount(drive, apiUrl, debugging);
            }
        }
        catch (Exception e)
        {
            if (debugging)
            {
                log.Fatal("Failed", e);
            }

            return ShowError(e);
        }

        return 0;
    }

    private static int ShowError(string message)
    {
        Console.WriteLine(message);
        Console.WriteLine("Try 'ipfs-mount --help' for more information.");
        return 1;
    }

    private static int ShowError(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            Console.WriteLine(e.Message);
        }

        return 1;
    }

    private static int ShowHelp(OptionSet p)
    {
        Console.WriteLine("Mount the IPFS on the specified drive");
        Console.WriteLine();
        Console.WriteLine("Usage: ipfs-mount drive [OPTIONS]");
        Console.WriteLine("Options:");
        p.WriteOptionDescriptions(Console.Out);
        return 0;
    }
}

public class Runner
{
    public static async Task Mount(string drive, string apiUrl, bool debugging)
    {
        if (!string.IsNullOrWhiteSpace(apiUrl))
        {
            IpfsClient.DefaultApiUri = new Uri(apiUrl);
        }

        // Verify that the local IPFS service is up and running
        var x = await new IpfsClient().IdAsync();

        // CTRL-C will dismount and then exit.
        Console.CancelKeyPress += (s, e) =>
        {
            Console.WriteLine("shutting down...");
            Unmount(drive);
            e.Cancel = true;
        };

        // Mount IPFS, doesn't return until the drive is dismounted
        var options = DokanOptions.WriteProtection;
        if (debugging)
        {
            options |= DokanOptions.DebugMode;
        }

        Dokan.Mount(new IpfsDokan(), drive, options, new DokanLogger());
    }

    public static void Unmount(string drive) => Dokan.Unmount(drive[0]);
}
