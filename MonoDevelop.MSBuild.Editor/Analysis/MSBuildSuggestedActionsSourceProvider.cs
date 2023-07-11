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
using MonoDevelop.Xml.Editor.Logging;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	[Export (typeof (ISuggestedActionsSourceProvider))]
	[Name ("MSBuild Suggested Actions")]
	[ContentType (MSBuildContentType.Name)]
	class MSBuildSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
	{
		readonly IEditorLoggerFactory loggerFactory;

		[ImportingConstructor]
		public MSBuildSuggestedActionsSourceProvider (
			IViewTagAggregatorFactoryService viewTagAggregatorFactoryService,
			JoinableTaskContext joinableTaskContext,
			ISuggestedActionCategoryRegistryService2 categoryRegistry,
			MSBuildParserProvider parserProvider,
			MSBuildCodeFixService codeFixService,
			MSBuildRefactoringService refactoringService,
			IEditorLoggerFactory loggerFactory,
			// allow default because we don't have a Mac version yet
			[Import (AllowDefault = true)] PreviewChangesService previewService,
			IMSBuildSuggestedActionFactory suggestedActionFactory
			)
		{
			ViewTagAggregatorFactoryService = viewTagAggregatorFactoryService;
			JoinableTaskContext = joinableTaskContext;
			CategoryRegistry = categoryRegistry;
			ParserProvider = parserProvider;
			CodeFixService = codeFixService;
			RefactoringService = refactoringService;
			this.loggerFactory = loggerFactory;
			PreviewService = previewService;
			SuggestedActionFactory = suggestedActionFactory;
		}

		public IViewTagAggregatorFactoryService ViewTagAggregatorFactoryService { get; }
		public JoinableTaskContext JoinableTaskContext { get; }
		public ISuggestedActionCategoryRegistryService2 CategoryRegistry { get; }
		public MSBuildParserProvider ParserProvider { get; }
		public MSBuildCodeFixService CodeFixService { get; }
		public MSBuildRefactoringService RefactoringService { get; }
		public PreviewChangesService PreviewService { get; }
		public IMSBuildSuggestedActionFactory SuggestedActionFactory { get; }

		public ISuggestedActionsSource CreateSuggestedActionsSource (ITextView textView, ITextBuffer textBuffer)
		{
			var logger = loggerFactory.CreateLogger<MSBuildSuggestedActionSource> (textView);
			return new MSBuildSuggestedActionSource (this, textView, textBuffer, logger);
		}
	}
}
