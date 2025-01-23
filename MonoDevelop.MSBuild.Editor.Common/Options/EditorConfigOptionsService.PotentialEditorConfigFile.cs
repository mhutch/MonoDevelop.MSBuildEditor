// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System.IO;
using System.Threading;
using Microsoft.CodeAnalysis.EditorConfig.Parsing;

namespace MonoDevelop.MSBuild.Editor.Options;

partial class EditorConfigOptionsService
{
	/// <summary>
	/// Represents an editorconfig file that may or may not exist.
	/// This allows us to enumerate locations for file change events
	/// that may affect the editorconfig hierarchy.
	/// </summary>
	class PotentialEditorConfigFile (EditorConfigOptionsService parent, string directory)
	{
		readonly bool isRootPath = Path.GetDirectoryName(directory) is null;

		public string FilePath { get; } = Path.Combine(directory, ".editorconfig");

		public string Directory { get; } = directory;

		public PotentialEditorConfigFile? Parent { get; set; }

		public EditorConfigFile<RawEditorConfigOption>? ParsedFile { get; set; }

		public bool IsRoot => isRootPath || Parent is null;

		public void AddRef()
		{
			Interlocked.Increment(ref refCount);
		}

		public void ReleaseRef()
		{
			if (Interlocked.Decrement(ref refCount) == 0) {
				lock(parent.editorConfigFileByDirectory) {
					parent.editorConfigFileByDirectory.Remove(Directory);
				}
			}
		}

		int refCount = 0;
	}
}
