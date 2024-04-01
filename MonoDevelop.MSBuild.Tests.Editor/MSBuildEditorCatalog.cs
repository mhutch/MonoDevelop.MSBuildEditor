// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.MiniEditor;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Editor.Tests;

namespace MonoDevelop.MSBuild.Tests;

public class MSBuildEditorCatalog : EditorCatalog
{
	public MSBuildEditorCatalog (EditorEnvironment.Host host) : base (host) { }

	internal MSBuildParserProvider MSBuildParserProvider => GetService<MSBuildParserProvider> ();
}
