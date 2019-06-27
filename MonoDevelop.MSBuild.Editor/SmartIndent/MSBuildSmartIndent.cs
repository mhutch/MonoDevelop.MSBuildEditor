// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Editor.Completion;

namespace MonoDevelop.MSBuild.Editor.SmartIndent
{
	class MSBuildSmartIndent : XmlSmartIndent<MSBuildBackgroundParser,MSBuildParseResult>
	{
		// FIXME: import and read additional options from Microsoft.VisualStudio.CodingConventions.ICodingConventionsManager
		public MSBuildSmartIndent (ITextView textView) : base (textView)
		{
		}
	}
}