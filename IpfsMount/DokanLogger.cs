namespace Ipfs.VirtualDisk;

using Common.Logging;

/// <summary>
/// Maps Dokan logging to Common Logging.
/// </summary>
internal class DokanLogger : DokanNet.Logging.ILogger
{
    private static readonly ILog log = LogManager.GetLogger("Dokan");

    public bool DebugEnabled => log.IsDebugEnabled;

    public void Debug(string message, params object[] args)
    {
        if (!log.IsDebugEnabled)
        {
            return;
        }

        if (args.Length > 0)
        {
            log.DebugFormat(message, args);
        }
        else
        {
            log.Debug(message);
        }
    }

    public void Error(string message, params object[] args)
    {
        if (!log.IsErrorEnabled)
        {
            return;
        }

        if (args.Length > 0)
        {
            log.ErrorFormat(message, args);
        }
        else
        {
            log.Error(message);
        }
    }

    public void Fatal(string message, params object[] args)
    {
        if (!log.IsFatalEnabled)
        {
            return;
        }

        if (args.Length > 0)
        {
            log.FatalFormat(message, args);
        }
        else
        {
            log.Fatal(message);
        }
    }

    public void Info(string message, params object[] args)
    {
        if (!log.IsInfoEnabled)
        {
            return;
        }

        if (args.Length > 0)
        {
            log.InfoFormat(message, args);
        }
        else
        {
            log.Info(message);
        }
    }

    public void Warn(string message, params object[] args)
    {
        if (!log.IsWarnEnabled)
        {
            return;
        }

        if (args.Length > 0)
        {
            log.WarnFormat(message, args);
        }
        else
        {
            log.Warn(message);
        }
    }
}
