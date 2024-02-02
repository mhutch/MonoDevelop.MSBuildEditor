// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.Xml.Editor.Logging;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	[Export (typeof (IAsyncCompletionCommitManagerProvider))]
	[Name ("MSBuild Completion Commit Manager Provider")]
	[ContentType (MSBuildContentType.Name)]
	class MSBuildCompletionCommitManagerProvider : IAsyncCompletionCommitManagerProvider
	{
		[ImportingConstructor]
		public MSBuildCompletionCommitManagerProvider (
			JoinableTaskContext joinableTaskContext,
			IEditorCommandHandlerServiceFactory commandServiceFactory,
			IEditorLoggerFactory loggerService)
		{
			JoinableTaskContext = joinableTaskContext;
			CommandServiceFactory = commandServiceFactory;
			LoggerService = loggerService;
		}

		public JoinableTaskContext JoinableTaskContext { get; }
		public IEditorCommandHandlerServiceFactory CommandServiceFactory { get; }
		public IEditorLoggerFactory LoggerService { get; }

		public IAsyncCompletionCommitManager GetOrCreate (ITextView textView) =>
			textView.Properties.GetOrCreateSingletonProperty (
				typeof (MSBuildCompletionCommitManager), () => {
					var logger = LoggerService.CreateLogger<MSBuildCompletionSource> (textView);
					return new MSBuildCompletionCommitManager (logger, JoinableTaskContext, CommandServiceFactory);
				}
			);
	}
}