// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Xml.Options;

namespace MonoDevelop.MSBuild.Editor;

class MSBuildEditorOptions
{
	// this maps to a VS option
	public static readonly Option<bool> ReplicateNewlineCharacter = new ("replicate_newline_character", true, false);
}
