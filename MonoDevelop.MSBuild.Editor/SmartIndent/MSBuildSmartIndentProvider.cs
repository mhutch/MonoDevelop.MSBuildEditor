// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MonoDevelop.MSBuild.Editor.SmartIndent
{
	[Export (typeof (ISmartIndentProvider))]
	[ContentType (MSBuildContentType.Name)]
	class MSBuildSmartIndentProvider : ISmartIndentProvider
	{
		public ISmartIndent CreateSmartIndent (ITextView textView)
		{
			return textView.Properties.GetOrCreateSingletonProperty (() => new MSBuildSmartIndent (textView));
		}
	}
}
