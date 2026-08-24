using BitcoderCZ.IO;
using Microsoft.Extensions.Logging;
using Solace.Common;

namespace Solace.Buildplate.Common;

public static partial class ServerUtils
{
    public static async Task WaitForSetup(AbsoluteDirectory staticDataPath, ILogger logger, CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken); // give time to server setup to start and lock the file

        var fileLock = new FileLock(staticDataPath / "server_template_dir" / new RelativeFile(".setupLock"));

        var setupDoneFile = staticDataPath / "server_template_dir" / new RelativeFile(".setupDone");

        if (setupDoneFile.Exists)
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
                if (setupDoneFile.Exists)
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
