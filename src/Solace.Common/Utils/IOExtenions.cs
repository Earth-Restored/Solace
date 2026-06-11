using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Solace.Common.Utils;

#pragma warning disable CA1708 // Identifiers should differ by more than case
public static class IOExtenions
#pragma warning restore CA1708 // Identifiers should differ by more than case
{
    extension(ZipArchiveEntry entry)
    {
        public bool IsDirectory => entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\') || entry.Name == string.Empty;
    }

    extension(File)
    {
        public static FileStream OpenWriteNew(string path)
            => File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);

        /// <summary>
        /// Finds the newest file matching the template that satisfies the version constraints:
        /// Must have the exact same Major version, and a Minor/Patch version greater than or equal to the minVersion.
        /// </summary>
        /// <param name="directory">The directory to search inside.</param>
        /// <param name="minVersion">The minimum allowed version.</param>
        /// <param name="template">The filename template containing "{{version}}".</param>
        /// <param name="path">The full path to the newest compatible file, or null if none are found.</param>
        public static bool TryFindCompatibleFile(string directory, Version minVersion, string template, [MaybeNullWhen(false)] out string path)
        {
            if (!Directory.Exists(directory))
            {
                path = null;
                return false;
            }

            const string VersionPlaceholder = "___VERSION_PLACEHOLDER___";
            string templateWithToken = template.Replace("{{version}}", VersionPlaceholder);
            string escapedTemplate = Regex.Escape(templateWithToken);

            string pattern = "^" + escapedTemplate.Replace(VersionPlaceholder, @"(?<version>\d+(?:\.\d+)+)") + "$";

            var regex = new Regex(pattern, RegexOptions.CultureInvariant);

            var bestMatch = Directory.EnumerateFiles(directory)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Select(name => new { Name = name, Match = regex.Match(name) })
                .Where(x => x.Match.Success)
                .Select(x =>
                {
                    if (Version.TryParse(x.Match.Groups["version"].Value, out var parsedVersion))
                    {
                        if (minVersion.Revision != -1 && parsedVersion.Revision == -1)
                        {
                            parsedVersion = new Version(parsedVersion.Major, parsedVersion.Minor, parsedVersion.Build, 0);
                        }

                        return (x.Name, Version: parsedVersion);
                    }

                    return ((string Name, Version Version)?)null;
                })
                .Where(x => x is { } tupple &&
                    tupple.Version.Major == minVersion.Major &&
                    tupple.Version >= minVersion)
                .OrderByDescending(x => x!.Value.Version)
                .FirstOrDefault();

            if (bestMatch is null)
            {
                path = null;
                return false;
            }

            path = Path.Combine(directory, bestMatch.Value.Name);
            return true;
        }
    }

    extension(Path)
    {
        public static string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return filePath;
            }

            var directory = Path.GetDirectoryName(filePath)!;
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);

            int count = 1;
            string uniquePath;

            do
            {
                var newFileName = $"{fileNameWithoutExtension} {count}{extension}";
                uniquePath = Path.Combine(directory, newFileName);
                count++;
            }
            while (File.Exists(uniquePath));

            return uniquePath;
        }
    }

    extension(FileInfo file)
    {
        public long SafeLength
        {
            get
            {
                if (!file.Exists)
                {
                    return 0;
                }

                return file.Length;
            }
        }

        public FileStream OpenWriteNew()
           => File.Open(file.FullName, FileMode.Create, FileAccess.Write, FileShare.Read);

        public void SafeDelete()
        {
            try
            {
                file.Delete();
            }
            catch (DirectoryNotFoundException)
            {

            }
        }

        public bool CanExecute()
        {
            // TODO: implement

            try
            {
                if (!file.Exists)
                {
                    return false;
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }

    extension(DirectoryInfo directory)
    {
        public long Length => directory.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);

        public long SafeLength
        {
            get
            {
                if (!directory.Exists)
                {
                    return 0;
                }

                return directory.Length;
            }
        }

        public bool TryCreate()
        {
            try
            {
                directory.Create();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public bool CanRead()
        {
            // TODO: implement
            if (!directory.Exists)
            {
                return false;
            }

            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return true;
            }

            return true;
        }

        public void SafeDelete()
        {
            try
            {
                directory.Delete();
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        public void SafeDelete(bool recursive)
        {
            try
            {
                directory.Delete(recursive);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }
}