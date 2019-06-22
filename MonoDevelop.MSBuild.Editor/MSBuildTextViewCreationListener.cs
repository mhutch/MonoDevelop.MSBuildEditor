// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Editor.Completion;

namespace MonoDevelop.MSBuild.Editor
{
	[Export (typeof (ITextViewCreationListener))]
	[Name ("MSBuild TextView Creation Listener")]
	[ContentType (MSBuildContentType.Name)]
	[TextViewRole (PredefinedTextViewRoles.Editable)]
	sealed class MSBuildTextViewCreationListener : ITextViewCreationListener
	{
		public MSBuildTextViewCreationListener ()
		{
		}

		public void TextViewCreated (ITextView textView)
		{
			var buffer = (ITextBuffer2)textView.TextBuffer;

			// attach the parser to the buffer so it's ready when other things need it
			var parser = BackgroundParser<MSBuildParseResult>.GetParser<MSBuildBackgroundParser> (buffer);
			parser.GetOrParseAsync ((ITextSnapshot2)buffer.CurrentSnapshot, CancellationToken.None);
		}
	}
}
