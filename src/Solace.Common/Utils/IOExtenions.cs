using System.IO.Compression;

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