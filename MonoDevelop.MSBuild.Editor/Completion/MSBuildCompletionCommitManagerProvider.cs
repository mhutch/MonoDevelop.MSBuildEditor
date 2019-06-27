// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	[Export (typeof (IAsyncCompletionCommitManagerProvider))]
	[Name ("MSBuild Completion Commit Manager Provider")]
	[ContentType (MSBuildContentType.Name)]
	class MSBuildCompletionCommitManagerProvider : IAsyncCompletionCommitManagerProvider
	{
		[Import]
		public JoinableTaskContext JoinableTaskContext { get; set; }

		[Import]
		public IEditorCommandHandlerServiceFactory CommandServiceFactory { get; set; }

		public IAsyncCompletionCommitManager GetOrCreate (ITextView textView) =>
			textView.Properties.GetOrCreateSingletonProperty (
				typeof (MSBuildCompletionCommitManager), () => new MSBuildCompletionCommitManager (this)
			);
	}
}