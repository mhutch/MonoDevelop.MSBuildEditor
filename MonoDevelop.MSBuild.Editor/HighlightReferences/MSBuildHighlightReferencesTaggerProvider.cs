// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Editor.Tags;

namespace MonoDevelop.MSBuild.Editor.HighlightReferences
{
	[Export(typeof(IViewTaggerProvider))]
	[ContentType (MSBuildContentType.Name)]
	[TagType (typeof (NavigableHighlightTag))]
	[TextViewRole (PredefinedTextViewRoles.Interactive)]
	class MSBuildHighlightReferencesTaggerProvider : IViewTaggerProvider
	{
		[Import]
		public JoinableTaskContext JoinableTaskContext { get; set; }

		[Import]
		public IFunctionTypeProvider FunctionTypeProvider { get; set; }

		[Import]
		public MSBuildParserProvider ParserProvider { get; set; }

		public ITagger<T> CreateTagger<T> (ITextView textView, ITextBuffer buffer) where T : ITag
			=>  (ITagger<T>) buffer.Properties.GetOrCreateSingletonProperty (
				() => new MSBuildHighlightReferencesTagger (textView, this)
			);
	}
}
