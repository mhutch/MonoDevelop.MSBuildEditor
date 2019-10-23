// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Schema
{
	public class ValueKindValue : BaseInfo
	{
		public ValueKindValue (string name, DisplayText description, MSBuildValueKind kind) : base (name, description)
		{
			this.ValueKind = kind;
		}

		public MSBuildValueKind ValueKind { get; }
	}

	public sealed class FileOrFolderInfo : BaseInfo
	{
		public bool IsFolder { get; }

		public FileOrFolderInfo (string name, bool isDirectory, DisplayText description) : base (name, description)
		{
			IsFolder = isDirectory;
		}
	}
}