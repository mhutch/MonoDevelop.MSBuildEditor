// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using ProjectFileTools.NuGetSearch.IO;

namespace ProjectFileTools.Exports;

[Export(typeof(IFileSystem))]
[Name("Default File System Implementation")]
internal class ExportedFileSystem : FileSystem
{
}
