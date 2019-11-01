// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Editor.Completion;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	[Export (typeof (ISuggestedActionsSourceProvider))]
	[Name ("MSBuild Suggested Actions")]
	[ContentType (MSBuildContentType.Name)]
	class MSBuildSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
	{
		[Import]
		public IViewTagAggregatorFactoryService ViewTagAggregatorFactoryService { get; set; }

		[Import]
		public JoinableTaskContext JoinableTaskContext { get; set; }

		[Import]
		public MSBuildParserProvider ParserProvider { get; set; }

		[Import]
		public MSBuildCodeFixService CodeFixService { get; set; }

		[Import]
		public MSBuildRefactoringService RefactoringService { get; set; }

		// allow default because we don't have a Mac version yet
		[Import (AllowDefault = true)]
		public PreviewChangesService PreviewService { get; set; }

		[Import]
		public IMSBuildSuggestedActionFactory SuggestedActionFactory { get; set; }

		public ISuggestedActionsSource CreateSuggestedActionsSource (ITextView textView, ITextBuffer textBuffer)
		{
			return new MSBuildSuggestedActionSource (this, textView, textBuffer);
		}
	}
}
