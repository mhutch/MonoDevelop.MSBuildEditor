// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Editor.Tagging;

namespace MonoDevelop.MSBuild.Editor.HighlightReferences
{
	[Export(typeof(IViewTaggerProvider))]
	[ContentType (MSBuildContentType.Name)]
	[TagType (typeof (NavigableHighlightTag))]
	[TextViewRole (PredefinedTextViewRoles.Interactive)]
	class MSBuildHighlightReferencesTaggerProvider : IViewTaggerProvider
	{
		[ImportingConstructor]
		public MSBuildHighlightReferencesTaggerProvider (
			JoinableTaskContext joinableTaskContext,
			IFunctionTypeProvider functionTypeProvider,
			MSBuildParserProvider parserProvider,
			IEditorLoggerFactory loggerService)
		{
			JoinableTaskContext = joinableTaskContext;
			FunctionTypeProvider = functionTypeProvider;
			ParserProvider = parserProvider;
			LoggerService = loggerService;
		}

		public JoinableTaskContext JoinableTaskContext { get; }
		public IFunctionTypeProvider FunctionTypeProvider { get; }
		public MSBuildParserProvider ParserProvider { get; }
		public IEditorLoggerFactory LoggerService { get; }

		public ITagger<T> CreateTagger<T> (ITextView textView, ITextBuffer buffer) where T : ITag
			=>  (ITagger<T>) buffer.Properties.GetOrCreateSingletonProperty (() => {
				var logger = LoggerService.CreateLogger<MSBuildHighlightReferencesTagger> (textView);
				return new MSBuildHighlightReferencesTagger (textView, this, logger);
			});
	}
}
