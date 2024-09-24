// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Xml.Options;

namespace MonoDevelop.MSBuild.Options;

class MSBuildEditorOptions
{
	// this maps to a VS option
	/// <summary>
	/// Whether to replicate the previous line's newline character when inserting a new line.
	/// If false, then the file's default newline character should be used.
	/// </summary>
	public static readonly Option<bool> ReplicateNewlineCharacter = new ("replicate_newline_character", true, false);
}
