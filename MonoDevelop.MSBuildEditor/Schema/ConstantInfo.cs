// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuildEditor.Schema
{
	class ConstantInfo : BaseInfo
	{
		public ConstantInfo (string name, DisplayText description) : base (name, description)
		{
		}
	}

	class FileOrFolderInfo : BaseInfo
	{
		public bool IsFolder { get; }

		public FileOrFolderInfo (string name, bool isDirectory, DisplayText description) : base (name, description)
		{
			IsFolder = isDirectory;
		}
	}
}