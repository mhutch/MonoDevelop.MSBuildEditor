// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Editor.CodeActions;

class MSBuildDocumentDelete (string fileOrFolder) : MSBuildWorkspaceEditOperation (fileOrFolder)
{
	public string FileOrFolder => Filename;

	public bool Recursive { get; set; }

	public bool IgnoreIfNotExists { get; set; }

	public MSBuildChangeAnnotation? Annotation { get; set; }
}