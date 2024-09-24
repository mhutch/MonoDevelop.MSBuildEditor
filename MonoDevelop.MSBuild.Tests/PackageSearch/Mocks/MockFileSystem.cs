// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using ProjectFileTools.NuGetSearch.IO;

namespace ProjectFileTools.NuGetSearch.Tests;

public class MockFileSystem : IFileSystem
{
    public bool DirectoryExists(string path)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<string> EnumerateDirectories(string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<string> EnumerateFiles(string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        throw new NotImplementedException();
    }

    public bool FileExists(string path)
    {
        throw new NotImplementedException();
    }

    public string GetDirectoryName(string path)
    {
        throw new NotImplementedException();
    }

    public string GetDirectoryNameOnly(string path)
    {
        throw new NotImplementedException();
    }

    public string ReadAllText(string path)
    {
        throw new NotImplementedException();
    }
}
