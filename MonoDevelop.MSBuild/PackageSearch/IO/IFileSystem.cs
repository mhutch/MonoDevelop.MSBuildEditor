// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

namespace ProjectFileTools.NuGetSearch.IO;

public interface IFileSystem
{
    IEnumerable<string> EnumerateFiles(string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

    IEnumerable<string> EnumerateDirectories(string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

    string ReadAllText(string path);

    bool DirectoryExists(string path);

    bool FileExists(string path);

    string GetDirectoryName(string path);

    string GetDirectoryNameOnly(string path);
}
