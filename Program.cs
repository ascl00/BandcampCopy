using System;
using System.IO;
using System.IO.Compression;

namespace BandcampExpand
{
    enum CompressedFileType
    {
        FLAC,
        AAC,
        Unknown
    }

    class Program
    {
        // Peek inside the archive to see if we can recognise any of the file types.
        // NOTE that this method assumes there is not a mix of FLAC and AAC inside a single
        // archive.
        static CompressedFileType GetCompressedFileType(FileInfo file)
        {
            using (FileStream zipToOpen = new FileStream(file.FullName, FileMode.Open))
            {
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
                {
                    foreach(ZipArchiveEntry entry in archive.Entries)
                    {
                        string fileName = entry.FullName;

                        if(fileName.EndsWith(".FLAC", StringComparison.OrdinalIgnoreCase))
                        {
                            return CompressedFileType.FLAC;
                        }
                        else if(fileName.EndsWith(".AAC", StringComparison.OrdinalIgnoreCase))
                        {
                            return CompressedFileType.AAC;
                        }
                        else if (fileName.EndsWith(".M4A", StringComparison.OrdinalIgnoreCase))
                        {
                            return CompressedFileType.AAC;
                        }
                    }
                }
            }

            return CompressedFileType.Unknown;
        }

        static void ExpandCompressedFileToPath(FileInfo file, string destPath)
        {
            Console.WriteLine("Expanding " + file.Name + " to " + destPath);
            string zipPath = file.FullName;

            // Normalizes the path.
            string extractPath = Path.GetFullPath(destPath);

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // Gets the full path to ensure that relative segments are removed.
                    string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));

                    // Ordinal match is safest, case-sensitive volumes can be mounted within volumes that
                    // are case-insensitive.
                    if (destinationPath.StartsWith(extractPath, StringComparison.Ordinal))
                    {
                        Console.WriteLine("Writing " + entry.Name);
                        entry.ExtractToFile(destinationPath);
                    }
                }
            }
        }

        static string GetPathForFileType(FileInfo file, string musicFolder)
        {
            switch(GetCompressedFileType(file))
            {
                case CompressedFileType.FLAC:
                    Console.WriteLine("Found FLAC files in " + file.Name);
                    return musicFolder + "/FLAC";
                case CompressedFileType.AAC:
                    Console.WriteLine("Found AAC files in " + file.Name);
                    return musicFolder + "/AAC";
                default:
                    throw new FileNotFoundException("Cannot find known file type inside compressed folder");
            }
        }

        // Taken from https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        // Filename is usually something like: Psycroptic - As the Kingdom Drowns (pre-order)
        static string GetBandFromFilename(string filename)
        {
            const string separator = " - ";
            int startOfSeparator = filename.IndexOf(separator);

            string band = filename.Substring(0, startOfSeparator);
            return band;
        }

        private static bool IsDigit(char c)
        {
            return ((c >= '0') && (c <= '9'));
        }

        // Filename is usually something like: Psycroptic - As the Kingdom Drowns (pre-order)
        // Although it could also have a trailing number: Psycroptic - As the Kingdom Drowns (pre-order) (1)
        static string GetAlbumFromFilename(string filename)
        {
            const string separator = " - ";
            int startOfSeparator = filename.IndexOf(separator);
            int endOfSeparator = startOfSeparator + separator.Length;

            string albumNameWithFileExtension = filename.Substring(endOfSeparator);
            string albumName = albumNameWithFileExtension.Remove(albumNameWithFileExtension.Length - 4);

            // Check for trailing number (2)
            string last4Chars = albumName.Substring(albumName.Length - 4);
            if(last4Chars.StartsWith(" (") && last4Chars.EndsWith(")") && IsDigit(last4Chars[2]))
            {
                // Yeah we have some trash on the end, strip it
                albumName = albumName.Substring(0,albumName.Length - 4);
            }
            return albumName;
        }

        static void ProcessCompressedFile(FileInfo file, string musicFolder)
        {
            string tempPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Downloads/Bandcamp/auto";

            string bandName = GetBandFromFilename(file.Name);
            string bandTempPath = tempPath + "/" + bandName;
            string albumName = GetAlbumFromFilename(file.Name);
            string bandDestPath = GetPathForFileType(file, musicFolder) + "/" + bandName;

            string tempExtractionPath = tempPath + "/" + bandName + "/" + albumName;
            Console.WriteLine("Creating temp directory: " + tempExtractionPath);
            Directory.CreateDirectory(tempExtractionPath);
            ExpandCompressedFileToPath(file, tempExtractionPath);

            Console.WriteLine("Moving temp directory to final location");
            DirectoryCopy(bandTempPath, bandDestPath, true);
            Directory.Delete(bandTempPath, true);
        }

        static void Main(string[] args)
        {
            string musicFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + "/";
            string sourceFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Downloads/Bandcamp";

            // Bandcamp downloads are zipped, so lets see if we can find any
            DirectoryInfo d = new DirectoryInfo(sourceFolder);
            FileInfo[] Files = d.GetFiles("*.zip");
            foreach (FileInfo file in Files)
            {
                Console.WriteLine("Processing file: " + file.Name);
                ProcessCompressedFile(file, musicFolder);
                file.Delete();
            }
        }
    }
}
