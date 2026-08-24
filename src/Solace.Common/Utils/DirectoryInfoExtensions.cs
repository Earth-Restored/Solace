using BitcoderCZ.IO;

namespace Solace.Common.Utils;

public static class DirectoryInfoExtensions
{
    private static readonly EnumerationOptions FastEnumOptions = new()
    {
        MatchCasing = MatchCasing.PlatformDefault,
        MatchType = MatchType.Simple,
        AttributesToSkip = FileAttributes.None,
        IgnoreInaccessible = true
    };

    extension(DirectoryInfo directoryInfo)
    {
        public void CopyTo(string destDirectoryName, bool recursive = true)
        {
            if (!directoryInfo.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory not found: {directoryInfo.FullName}");
            }

            var subDirs = directoryInfo.GetDirectories();

            Directory.CreateDirectory(destDirectoryName);

            foreach (var file in directoryInfo.GetFiles())
            {
                var targetFilePath = Path.Combine(destDirectoryName, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            if (recursive)
            {
                foreach (var subDir in subDirs)
                {
                    var newDestDir = Path.Combine(destDirectoryName, subDir.Name);
                    subDir.CopyTo(newDestDir, true);
                }
            }
        }

        public void CopyFilesTo(string destinationDirectoryName, ReadOnlySpan<string> filesToCopy, bool overwrite = false)
        {
            ArgumentNullException.ThrowIfNull(directoryInfo);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectoryName);

            if (!directoryInfo.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist: '{directoryInfo.FullName}'");
            }

            if (filesToCopy.IsEmpty)
            {
                return;
            }

            Directory.CreateDirectory(destinationDirectoryName);

            var processedFiles = filesToCopy.Length > 1
                ? new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                : null;

            foreach (var pattern in filesToCopy)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                var patternSpan = pattern.AsSpan();

                if (patternSpan.IndexOfAny('*', '?') >= 0)
                {
                    foreach (var file in directoryInfo.EnumerateFiles(pattern, FastEnumOptions))
                    {
                        if (processedFiles is not null && !processedFiles.Add(file.FullName))
                        {
                            continue;
                        }

                        var destPath = Path.Combine(destinationDirectoryName, file.Name);
                        file.CopyTo(destPath, overwrite);
                    }
                }
                else
                {
                    var sourceFilePath = Path.Combine(directoryInfo.FullName, pattern);
                    if (File.Exists(sourceFilePath))
                    {
                        if (processedFiles is not null && !processedFiles.Add(sourceFilePath))
                        {
                            continue;
                        }

                        var fileName = Path.GetFileName(pattern);
                        var destPath = Path.Combine(destinationDirectoryName, fileName);
                        File.Copy(sourceFilePath, destPath, overwrite);
                    }
                }
            }
        }
    }

    extension(AbsoluteDirectory directory)
    {
        public void CopyFilesTo(AbsoluteDirectory destinationDirectory, ReadOnlySpan<string> filesToCopy, bool overwrite = false)
        {
            ArgumentNullException.ThrowIfNull(directory);
            ArgumentNullException.ThrowIfNull(destinationDirectory);

            if (!directory.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist: '{directory.Value}'");
            }

            if (filesToCopy.IsEmpty)
            {
                return;
            }

            destinationDirectory.Create();

            var processedFiles = filesToCopy.Length > 1
                ? new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                : null;

            foreach (var pattern in filesToCopy)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                var patternSpan = pattern.AsSpan();

                if (patternSpan.IndexOfAny('*', '?') >= 0)
                {
                    foreach (var file in directory.EnumerateFiles(FastEnumOptions, pattern))
                    {
                        if (processedFiles is not null && !processedFiles.Add(file.Value))
                        {
                            continue;
                        }

                        var destPath = destinationDirectory / new RelativeFile(file.Name);
                        file.CopyTo(destPath, overwrite);
                    }
                }
                else
                {
                    var sourceFilePath = directory / new RelativeFile(pattern);
                    if (sourceFilePath.Exists)
                    {
                        if (processedFiles is not null && !processedFiles.Add(sourceFilePath.Value))
                        {
                            continue;
                        }

                        var fileName = Path.GetFileName(pattern);
                        var destPath = destinationDirectory / new RelativeFile(fileName);
                        sourceFilePath.CopyTo(destPath, overwrite);
                    }
                }
            }
        }
    }
}