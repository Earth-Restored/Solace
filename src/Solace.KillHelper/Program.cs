using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Solace.KillHelper;

internal static partial class Program
{
    private static int Main(string[] args)
    {
        Debug.Assert(OperatingSystem.IsWindows());

        var processId = int.Parse(args[0], CultureInfo.InvariantCulture);

        FreeConsole(); // free our console
        if (AttachConsole((uint)processId))
        {
            SetConsoleCtrlHandler(null, true);
            try
            {
                if (!GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
                {
                    return 2;
                }
            }
            finally
            {
                SetConsoleCtrlHandler(null, false);
                FreeConsole();
            }

            return 0;
        }
        else
        {
            return 1;
        }
    }

#pragma warning disable CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(uint dwProcessId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeConsole();

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? handler, [MarshalAs(UnmanagedType.Bool)] bool add);

    private delegate bool ConsoleCtrlDelegate(uint ctrlType);
#pragma warning restore CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes

    private const uint CTRL_C_EVENT = 0;
}