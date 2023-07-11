// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Editor.Logging;

namespace MonoDevelop.MSBuild.Editor
{
	[Export (typeof (ITaggerProvider))]
	[ContentType (MSBuildContentType.Name)]
	[TagType (typeof (IErrorTag))]
	[TextViewRole (PredefinedTextViewRoles.Analyzable)]

	class MSBuildValidationTaggerProvider : ITaggerProvider
	{
		readonly JoinableTaskContext joinableTaskContext;
		readonly MSBuildParserProvider parserProvider;
		readonly IEditorLoggerFactory loggerFactory;

		[ImportingConstructor]
		public MSBuildValidationTaggerProvider (JoinableTaskContext joinableTaskContext, MSBuildParserProvider parserProvider, IEditorLoggerFactory loggerFactory)
		{
			this.joinableTaskContext = joinableTaskContext;
			this.parserProvider = parserProvider;
			this.loggerFactory = loggerFactory;
		}

		public ITagger<T> CreateTagger<T> (ITextBuffer buffer) where T : ITag
			=> (ITagger<T>)buffer.Properties.GetOrCreateSingletonProperty (() => {
				var logger = loggerFactory.GetLogger<MSBuildValidationTaggerProvider> (buffer);
				return new MSBuildValidationTagger (buffer, joinableTaskContext, parserProvider, logger);
			});

	}
}
