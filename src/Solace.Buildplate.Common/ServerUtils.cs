using Microsoft.Extensions.Logging;
using Solace.Common;

namespace Solace.Buildplate.Common;

public static partial class ServerUtils
{
    public static async Task WaitForSetup(string staticDataPath, ILogger logger, CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken); // give time to server setup to start and lock the file

        var fileLock = new FileLock(new FileInfo(Path.Combine(staticDataPath, "server_template_dir", ".setupLock")));

        var setupDoneFile = Path.Combine(staticDataPath, "server_template_dir", ".setupDone");

        if (File.Exists(setupDoneFile))
        {
            return;
        }

        LogWaitingForSetup(logger);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // needs to download files, user could have really slow internet
            var timeout = TimeSpan.FromMinutes(30);
            using (await fileLock.AcquireAsync(timeout, cancellationToken))
            {
                if (File.Exists(setupDoneFile))
                {
                    LogWaitForSetupDone(logger);
                    return;
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for the buildplate server to be setup, check buildplate-server-setup logs for progress")]
    private static partial void LogWaitingForSetup(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Server was setup")]
    private static partial void LogWaitForSetupDone(ILogger logger);
}
