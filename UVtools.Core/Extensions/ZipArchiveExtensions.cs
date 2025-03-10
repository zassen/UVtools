﻿/*
 *                     GNU AFFERO GENERAL PUBLIC LICENSE
 *                       Version 3, 19 November 2007
 *  Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
 *  Everyone is permitted to copy and distribute verbatim copies
 *  of this license document, but changing it is not allowed.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace UVtools.Core.Extensions;

public static class ZipArchiveExtensions
{
    /// <summary>
    /// Used to specify what our overwrite policy
    /// is for files we are extracting.
    /// </summary>
    public enum Overwrite
    {
        Always,
        IfNewer,
        Never
    }

    /// <summary>
    /// Used to identify what we will do if we are
    /// trying to create a zip file and it already
    /// exists.
    /// </summary>
    public enum ArchiveAction
    {
        Merge,
        Replace,
        Error,
        Ignore
    }

    /// <summary>
    /// Unzips the specified file to the given folder in a safe
    /// manner.  This plans for missing paths and existing files
    /// and handles them gracefully.
    /// </summary>
    /// <param name="sourceArchiveFileName">
    /// The name of the zip file to be extracted
    /// </param>
    /// <param name="destinationDirectoryName">
    /// The directory to extract the zip file to
    /// </param>
    /// <param name="overwriteMethod">
    /// Specifies how we are going to handle an existing file.
    /// The default is IfNewer.
    /// </param>
    public static void ImprovedExtractToDirectory(string sourceArchiveFileName, string destinationDirectoryName, Overwrite overwriteMethod = Overwrite.IfNewer)
    {
        //Opens the zip file up to be read
        using var archive = ZipFile.OpenRead(sourceArchiveFileName);
        archive.ImprovedExtractToDirectory(destinationDirectoryName, overwriteMethod);
    }

    /// <summary>
    /// Unzips the specified file to the given folder in a safe
    /// manner.  This plans for missing paths and existing files
    /// and handles them gracefully.
    /// </summary>
    /// <param name="archive">
    /// The zip file to be extracted
    /// </param>
    /// <param name="destinationDirectoryName">
    /// The directory to extract the zip file to
    /// </param>
    /// <param name="overwriteMethod">
    /// Specifies how we are going to handle an existing file.
    /// The default is IfNewer.
    /// </param>
    /// <returns>The number of extracted files</returns>
    public static int ImprovedExtractToDirectory(this ZipArchive archive, string destinationDirectoryName, Overwrite overwriteMethod = Overwrite.IfNewer)
    {
        int count = 0;
        //Loops through each file in the zip file
        foreach (var file in archive.Entries)
        {
            if(!string.IsNullOrEmpty(file.ImprovedExtractToFile(destinationDirectoryName, true, overwriteMethod))) count++;
        }

        return count;
    }

    /// <summary>
    /// Safely extracts a single file from a zip file
    /// </summary>
    /// <param name="entry">
    /// The zip entry we are pulling the file from
    /// </param>
    /// <param name="destinationPath">
    /// The root of where the file is going
    /// </param>
    /// <param name="preserveFullName">True to preserve full name and create all directories up to the file, otherwise false to extract the file just to <see cref="destinationPath"/></param>
    /// <param name="overwriteMethod">
    /// Specifies how we are going to handle an existing file.
    /// The default is Overwrite.IfNewer.
    /// </param>
    /// <returns>The extracted file path</returns>
    public static string? ImprovedExtractToFile(this ZipArchiveEntry entry, string destinationPath, bool preserveFullName = true, Overwrite overwriteMethod = Overwrite.IfNewer)
    {
        //Gets the complete path for the destination file, including any
        //relative paths that were in the zip file
        var destFileName = Path.GetFullPath(Path.Combine(destinationPath, preserveFullName ? entry.FullName : entry.Name));
        var fullDestDirPath = Path.GetFullPath(Path.Combine(destinationPath, (preserveFullName ? Path.GetDirectoryName(entry.FullName) : string.Empty)!) + Path.DirectorySeparatorChar);
        if (!destFileName.StartsWith(fullDestDirPath)) return null; // Entry is outside the target dir
        
        //Creates the directory (if it doesn't exist) for the new path
        Directory.CreateDirectory(fullDestDirPath);

        //Determines what to do with the file based upon the
        //method of overwriting chosen
        switch (overwriteMethod)
        {
            case Overwrite.Always:
                //Just put the file in and overwrite anything that is found
                entry.ExtractToFile(destFileName, true);
                break;
            case Overwrite.IfNewer:
                //Checks to see if the file exists, and if so, if it should
                //be overwritten
                if (!File.Exists(destFileName) || File.GetLastWriteTime(destFileName) < entry.LastWriteTime)
                {
                    //Either the file didn't exist or this file is newer, so
                    //we will extract it and overwrite any existing file
                    entry.ExtractToFile(destFileName, true);
                }
                break;
            case Overwrite.Never:
                //Put the file in if it is new but ignores the 
                //file if it already exists
                if (!File.Exists(destFileName))
                {
                    entry.ExtractToFile(destFileName);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(overwriteMethod), overwriteMethod, null);
        }

        return destFileName;
    }

    /// <summary>
    /// Allows you to add files to an archive, whether the archive
    /// already exists or not
    /// </summary>
    /// <param name="archiveFullName">
    /// The name of the archive to you want to add your files to
    /// </param>
    /// <param name="files">
    /// A set of file names that are to be added
    /// </param>
    /// <param name="action">
    /// Specifies how we are going to handle an existing archive
    /// </param>
    /// <param name="compression">
    /// Specifies what type of compression to use - defaults to Optimal
    /// </param>
    public static void AddToArchive(string archiveFullName,
        List<string> files,
        ArchiveAction action = ArchiveAction.Replace,
        Overwrite fileOverwrite = Overwrite.IfNewer,
        CompressionLevel compression = CompressionLevel.Optimal)
    {
        //Identifies the mode we will be using - the default is Create
        ZipArchiveMode mode = ZipArchiveMode.Create;

        //Determines if the zip file even exists
        bool archiveExists = File.Exists(archiveFullName);

        //Figures out what to do based upon our specified overwrite method
        switch (action)
        {
            case ArchiveAction.Merge:
                //Sets the mode to update if the file exists, otherwise
                //the default of Create is fine
                if (archiveExists)
                {
                    mode = ZipArchiveMode.Update;
                }
                break;
            case ArchiveAction.Replace:
                //Deletes the file if it exists.  Either way, the default
                //mode of Create is fine
                if (archiveExists)
                {
                    File.Delete(archiveFullName);
                }
                break;
            case ArchiveAction.Error:
                //Throws an error if the file exists
                if (archiveExists)
                {
                    throw new IOException(string.Format("The zip file {0} already exists.", archiveFullName));
                }
                break;
            case ArchiveAction.Ignore:
                //Closes the method silently and does nothing
                if (archiveExists)
                {
                    return;
                }
                break;
        }

        //Opens the zip file in the mode we specified
        using ZipArchive zipFile = ZipFile.Open(archiveFullName, mode);
        //This is a bit of a hack and should be refactored - I am
        //doing a similar foreach loop for both modes, but for Create
        //I am doing very little work while Update gets a lot of
        //code.  This also does not handle any other mode (of
        //which there currently wouldn't be one since we don't
        //use Read here).
        if (mode == ZipArchiveMode.Create)
        {
            foreach (string file in files)
            {
                //Adds the file to the archive
                zipFile.CreateEntryFromFile(file, Path.GetFileName(file), compression);
            }
        }
        else
        {
            foreach (string file in files)
            {
                var fileInZip = (from f in zipFile.Entries
                    where f.Name == Path.GetFileName(file)
                    select f).FirstOrDefault();

                switch (fileOverwrite)
                {
                    case Overwrite.Always:
                        //Deletes the file if it is found
                        if (fileInZip != null)
                        {
                            fileInZip.Delete();
                        }

                        //Adds the file to the archive
                        zipFile.CreateEntryFromFile(file, Path.GetFileName(file), compression);

                        break;
                    case Overwrite.IfNewer:
                        //This is a bit trickier - we only delete the file if it is
                        //newer, but if it is newer or if the file isn't already in
                        //the zip file, we will write it to the zip file
                        if (fileInZip != null)
                        {
                            //Deletes the file only if it is older than our file.
                            //Note that the file will be ignored if the existing file
                            //in the archive is newer.
                            if (fileInZip.LastWriteTime < File.GetLastWriteTime(file))
                            {
                                fileInZip.Delete();

                                //Adds the file to the archive
                                zipFile.CreateEntryFromFile(file, Path.GetFileName(file), compression);
                            }
                        }
                        else
                        {
                            //The file wasn't already in the zip file so add it to the archive
                            zipFile.CreateEntryFromFile(file, Path.GetFileName(file), compression);
                        }
                        break;
                    case Overwrite.Never:
                        //Don't do anything - this is a decision that you need to
                        //consider, however, since this will mean that no file will
                        //be written.  You could write a second copy to the zip with
                        //the same name (not sure that is wise, however).
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Get or put a file into archive
    /// </summary>
    /// <param name="input"><see cref="ZipArchive"/></param>
    /// <param name="filename">Filename to create</param>
    /// <returns>Created <see cref="ZipArchiveEntry"/></returns>
    public static ZipArchiveEntry GetPutFile(this ZipArchive input, string filename)
    {
        return input.GetEntry(filename) ?? input.CreateEntry(filename);
    }

    /// <summary>
    /// Create or update a file into archive and write content to it
    /// </summary>
    /// <param name="input"><see cref="ZipArchive"/></param>
    /// <param name="filename">Filename to create</param>
    /// <param name="content">Content to write</param>
    /// <param name="mode"></param>
    /// <returns>Created <see cref="ZipArchiveEntry"/></returns>
    public static ZipArchiveEntry PutFileContent(this ZipArchive input, string filename, string? content, ZipArchiveMode mode)
    {
        ZipArchiveEntry entry;
        if (mode == ZipArchiveMode.Update)
        {
            entry = input.GetEntry(filename) ?? input.CreateEntry(filename);
        }
        else
        {
            entry = input.CreateEntry(filename);
        }

        if (string.IsNullOrEmpty(content)) return entry;
        using var stream = entry.Open();
        if (mode == ZipArchiveMode.Update) stream.SetLength(0);
        using TextWriter tw = new StreamWriter(stream);
        tw.Write(content);
        return entry;
    }

    /// <summary>
    /// Create or update a file into archive and write content to it
    /// </summary>
    /// <param name="input"><see cref="ZipArchive"/></param>
    /// <param name="filename">Filename to create</param>
    /// <param name="content">Content to write</param>
    /// <param name="mode"></param>
    /// <returns>Created <see cref="ZipArchiveEntry"/></returns>
    public static ZipArchiveEntry PutFileContent(this ZipArchive input, string filename, byte[]? content, ZipArchiveMode mode)
    {
        ZipArchiveEntry entry;
        if (mode == ZipArchiveMode.Update)
        {
            entry = input.GetEntry(filename) ?? input.CreateEntry(filename);
        }
        else
        {
            entry = input.CreateEntry(filename);
        }

        if (content is null) return entry;
        using var stream = entry.Open();
        if (mode == ZipArchiveMode.Update) stream.SetLength(0);
        stream.Write(content, 0, content.Length);
        return entry;
    }

    /// <summary>
    /// Create or update a file into archive and write content to it
    /// </summary>
    /// <param name="input"><see cref="ZipArchive"/></param>
    /// <param name="filename">Filename to create</param>
    /// <param name="content">Content to write</param>
    /// <param name="mode"></param>
    /// <returns>Created <see cref="ZipArchiveEntry"/></returns>
    public static ZipArchiveEntry PutFileContent(this ZipArchive input, string filename, Stream? content, ZipArchiveMode mode)
    {
        ZipArchiveEntry entry;
        if (mode == ZipArchiveMode.Update)
        {
            entry = input.GetEntry(filename) ?? input.CreateEntry(filename);
        }
        else
        {
            entry = input.CreateEntry(filename);
        }

        if (content is null) return entry;
        using var stream = entry.Open();
        if (mode == ZipArchiveMode.Update) stream.SetLength(0);
        content.CopyTo(stream);
        return entry;
    }
}