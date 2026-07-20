using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Solace.Common.Utils;

namespace Solace.Common;

public static partial class JavaLocator
{
    public static string Locate(ILogger logger)
    {
        LogLocateBegin(logger);

        string? javaHome;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            javaHome = Environment.GetEnvironmentVariable("JAVA_HOME", EnvironmentVariableTarget.User);
            if (string.IsNullOrWhiteSpace(javaHome))
            {
                javaHome = Environment.GetEnvironmentVariable("JAVA_HOME", EnvironmentVariableTarget.Machine);
            }
        }
        else
        {
            javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        }

        if (!string.IsNullOrEmpty(javaHome))
        {
            LogTryJavaHome(logger);

            try
            {
                var file = new FileInfo(Path.Combine(javaHome, "bin", "java"));
                if (file.CanExecute())
                {
                    var path = file.FullName;
                    LogUseJavaHome(logger, path);
                    return path;
                }

                file = new FileInfo(Path.Combine(javaHome, "bin", "java.exe"));
                if (file.CanExecute())
                {
                    var path = file.FullName;
                    LogUseJavaHome(logger, path);
                    return path;
                }
            }
            catch (IOException)
            {
                // empty
            }

            LogJavaHomeNotSuitable(logger);
        }
        else
        {
            LogJavaHomeNotSet(logger);
        }

        LogUseJava(logger);
        return "java";
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Trying to locate Java")]
    private static partial void LogLocateBegin(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Trying JAVA_HOME")]
    private static partial void LogTryJavaHome(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Using Java from JAVA_HOME ({Path})")]
    private static partial void LogUseJavaHome(ILogger logger, string Path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Java from JAVA_HOME is not suitable (does not exist or cannot be accessed)")]
    private static partial void LogJavaHomeNotSuitable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "JAVA_HOME is not set")]
    private static partial void LogJavaHomeNotSet(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Using \"java\"")]
    private static partial void LogUseJava(ILogger logger);
}
