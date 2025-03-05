using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/*
using var loggerFactory = LoggerFactory.Create(static builder =>
{
    builder
        .AddDebug()
        .AddConsole();
});

ILogger log = loggerFactory.CreateLogger<Program>();
*/

namespace BandcampExpand
{
    enum CompressedFileType
    {
        FLAC,
        AAC,
        Unknown
    }

    public static class Program
    {

        public static ILogger log = LoggerFactory.Create(static builder => {
            builder
                .AddFilter("BandcampExpand", LogLevel.Debug)
                .AddDebug()
                .AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                })
                .AddConsole();
        }).CreateLogger("BandcampExpand");

        // Peek inside the archive to see if we can recognise any of the file types.
        // NOTE that this method assumes there is not a mix of FLAC and AAC inside a single
        // archive.
        static CompressedFileType GetCompressedFileType(FileInfo file)
        {
            using (FileStream zipToOpen = new FileStream(file.FullName, FileMode.Open))
            {
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string fileName = entry.FullName;

                        if (fileName.EndsWith(".FLAC", StringComparison.OrdinalIgnoreCase))
                        {
                            return CompressedFileType.FLAC;
                        }
                        else if (fileName.EndsWith(".AAC", StringComparison.OrdinalIgnoreCase))
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

        static public string StripIfStartsWith(string str, string match)
        {
            if (str.StartsWith(match, StringComparison.OrdinalIgnoreCase))
            {
                return str.Substring(match.Length);
            }
            return str;
        }

        static void ExpandCompressedFileToPath(FileInfo file, string destPath, string bandName, string albumName)
        {
            log.LogInformation("Expanding " + file.Name + " to " + destPath);
            string zipPath = file.FullName;

            // Normalizes the path.
            string extractPath = Path.GetFullPath(destPath);

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // Some archives contain stupid names (looking at you Lacerated Enemy Records)
                    // For example: Lacerated Enemy records - THE RITUAL AURA - Tæther - 01 Tæthered Betwixt - Hearthless.flac
                    // or
                    // NECROVILE - NECROVILE - Engorging The Devourmental Void - 01 I Kill Therefore I Am.flac
                    // Lets strip out anything silly
                    string fullname = StripIfStartsWith(entry.FullName, "Lacerated Enemy records - ");
                    fullname = StripIfStartsWith(fullname, bandName + " - ");
                    fullname = StripIfStartsWith(fullname, bandName + " - ");
                    fullname = StripIfStartsWith(fullname, albumName + " - ");

                    // Gets the full path to ensure that relative segments are removed.
                    string destinationPath = Path.GetFullPath(Path.Combine(extractPath, fullname));

                    // Ordinal match is safest, case-sensitive volumes can be mounted within volumes that
                    // are case-insensitive.
                    if (destinationPath.StartsWith(extractPath, StringComparison.Ordinal))
                    {
                        if (File.Exists(destinationPath))
                        {
                            log.LogWarning("Skipping (already exists) " + fullname);
                        }
                        else
                        {
                            log.LogDebug("Writing " + destinationPath);
                            entry.ExtractToFile(destinationPath);
                        }
                    }
                }
            }
        }

        static public string GetPathForFileType(FileInfo file)
        {
            switch (GetCompressedFileType(file))
            {
                case CompressedFileType.FLAC:
                    log.LogInformation("Found FLAC files in " + file.Name);
                    return "FLAC";
                case CompressedFileType.AAC:
                    log.LogInformation("Found AAC files in " + file.Name);
                    return "AAC";
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
                if (File.Exists(temppath))
                {
                    long dLen = new FileInfo(temppath).Length;
                    long sLen = file.Length;
                    if (dLen == sLen)
                    {
                        log.LogWarning("File: " + temppath + " already exists, skipping...");
                        continue;
                    }
                    else
                    {
                        log.LogError("File: " + temppath + " already exists, but has a filesize mismatch, copying again (" + sLen + " != " + dLen + ")");
                        File.Delete(temppath);
                    }
                }
                log.LogDebug("Copying from " + file.FullName + " to " + temppath);
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
            int startOfSeparator = filename.IndexOf(separator, System.StringComparison.CurrentCulture);

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
            int startOfSeparator = filename.IndexOf(separator, System.StringComparison.CurrentCulture);
            int endOfSeparator = startOfSeparator + separator.Length;

            string albumNameWithFileExtension = filename.Substring(endOfSeparator);
            string albumName = albumNameWithFileExtension.Remove(albumNameWithFileExtension.Length - 4);

            // Check for trailing number (2)
            if (albumName.Length > 4)
            {
                string last4Chars = albumName.Substring(albumName.Length - 4);
                if (last4Chars.StartsWith(" (", System.StringComparison.CurrentCulture) && last4Chars.EndsWith(")", System.StringComparison.CurrentCulture) && IsDigit(last4Chars[2]))
                {
                    // Yeah we have some trash on the end, strip it
                    albumName = albumName.Substring(0, albumName.Length - 4);
                }
            }
            return albumName;
        }

        static bool ProcessCompressedFile(FileInfo file, string musicFolder)
        {
            string tempPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Downloads/Bandcamp/auto";
            string bandName;
            try
            {
                bandName = GetBandFromFilename(file.Name);
            }
            catch (System.ArgumentOutOfRangeException e)
            {
                log.LogError("Failed to parse band from filename: " + file.Name + ". Is this a bandcamp download?");
                log.LogError(e.Message);
                return false;
            }
            
            string albumName;
            try
            {
                albumName = GetAlbumFromFilename(file.Name);
            }
            catch (System.ArgumentOutOfRangeException e)
            {
                log.LogError("Failed to parse album from filename: " + file.Name + ". Is this a bandcamp download?");
                log.LogError(e.Message);
                return false;
            }
            string albumType;
            try
            {
                albumType = GetPathForFileType(file);
            }
            catch (System.ArgumentOutOfRangeException e)
            {
                log.LogError("Failed to parse path from filename: " + file.Name + ". Is this a bandcamp download?");
                log.LogError(e.Message);
                return false;
            }
            string bandTempPath = tempPath + "/" + albumType + "/" + bandName;
            string tempExtractionPath = bandTempPath + "/" + albumName;

            string bandDestPath = musicFolder + "/" + albumType + "/" + bandName;

            log.LogInformation("Creating temp directory: " + tempExtractionPath);
            Directory.CreateDirectory(tempExtractionPath);
            ExpandCompressedFileToPath(file, tempExtractionPath, bandName, albumName);

            log.LogInformation("Moving " + bandTempPath + " to final location: " + bandDestPath);
            DirectoryCopy(bandTempPath, bandDestPath, true);
            Directory.Delete(bandTempPath, true);

            return true;
        }

        static void Main()
        {
            string musicFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + "/";
            string sourceFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Downloads/Bandcamp";

            // Bandcamp downloads are zipped, so lets see if we can find any
            DirectoryInfo d = new DirectoryInfo(sourceFolder);
            FileInfo[] Files = d.GetFiles("*.zip");
            BlockingCollection<string> processedFiles = new BlockingCollection<string>();

            Parallel.ForEach<FileInfo, string>(Files, // source collection
                () => null, // Method to initialize the local variables (noop)
                (file, loop, name) =>
                {
                    using (log.BeginScope(Thread.CurrentThread.ManagedThreadId.ToString()))
                    {
                        log.LogInformation("Processing file: " + file.Name);
                        if (ProcessCompressedFile(file, musicFolder))
                        {
                            file.Delete();
                            return file.Name;
                        }
                        return null;
                    }
                    ;
                },
                (name) =>
                {
                    if (!string.IsNullOrEmpty(name)) { processedFiles.Add(name); }
                }
            );
            log.LogInformation("");
            foreach (string name in processedFiles)
            {
                log.LogInformation("Successfully processed: " + name);
            }
            log.LogInformation("Press any key to end");
            Console.ReadKey();
        }
    }
}
