// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.MSBuild.Editor.Completion;

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

		[ImportingConstructor]
		public MSBuildValidationTaggerProvider (JoinableTaskContext joinableTaskContext, MSBuildParserProvider parserProvider)
		{
			this.joinableTaskContext = joinableTaskContext;
			this.parserProvider = parserProvider;
		}

		public ITagger<T> CreateTagger<T> (ITextBuffer buffer) where T : ITag
			=> (ITagger<T>)buffer.Properties.GetOrCreateSingletonProperty (() => new MSBuildValidationTagger (buffer, joinableTaskContext, parserProvider));
	}
}
