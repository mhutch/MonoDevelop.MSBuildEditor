// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProjectFileTools.NuGetSearch.IO;

public class FileSystem : IFileSystem
{
    public bool DirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // avoid a first-chance exception in Directory.Exists if we know it's a URL anyway
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.IndexOfAny(Path.GetInvalidPathChars()) < 0 && Directory.Exists(path);
    }

    public IEnumerable<string> EnumerateDirectories(string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (!DirectoryExists(path))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.EnumerateDirectories(path, pattern, searchOption);
    }

    public IEnumerable<string> EnumerateFiles(string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (!DirectoryExists(path))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.EnumerateFiles(path, pattern, searchOption);
    }

    public bool FileExists(string path)
    {
        return path.IndexOfAny(Path.GetInvalidPathChars()) < 0 && File.Exists(path);
    }

    public string GetDirectoryName(string path)
    {
        return Path.GetDirectoryName(path);
    }

    public string GetDirectoryNameOnly(string path)
    {
        return new DirectoryInfo(path).Name;
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }
}
