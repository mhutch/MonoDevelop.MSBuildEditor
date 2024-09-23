// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Editor.CodeActions;

class MSBuildDocumentCreate (string filename) : MSBuildWorkspaceEditOperation (filename)
{
	public string? Content { get; set; }

	public bool Overwrite { get; set; }

	public bool IgnoreIfExists { get; set; }

	public MSBuildChangeAnnotation? Annotation { get; set; }
}