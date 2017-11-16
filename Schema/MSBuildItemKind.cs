// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuildEditor.Schema
{
	public enum MSBuildItemKind
	{
		Unknown,
		String,
		File,
		Folder,
		SingleFile,
		SingleString,
		NuGetPackageID,
		Url,
	}
}
