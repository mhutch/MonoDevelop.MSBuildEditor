// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Editor.Logging;
using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	[Export (typeof (IAsyncCompletionSourceProvider))]
	[Name ("MSBuild Completion Source Provider")]
	[ContentType (MSBuildContentType.Name)]
	class MSBuildCompletionSourceProvider : IAsyncCompletionSourceProvider
	{
		[Import (typeof (IFunctionTypeProvider))]
		internal IFunctionTypeProvider FunctionTypeProvider { get; set; }


		[Import (typeof (IPackageSearchManager))]
		public IPackageSearchManager PackageSearchManager { get; set; }

		[Import]
		public DisplayElementFactory DisplayElementFactory { get; set; }

		[Import]
		public JoinableTaskContext JoinableTaskContext { get; set; }

		[Import]
		public MSBuildParserProvider ParserProvider { get; set; }

		[Import]
		public IEditorLoggerService EditorLoggerService { get; set; }

		[Import]
		public XmlParserProvider XmlParserProvider { get; set; }

		public IAsyncCompletionSource GetOrCreate (ITextView textView) =>
			textView.Properties.GetOrCreateSingletonProperty (
				typeof (MSBuildCompletionSource),
				() => new MSBuildCompletionSource (
					textView,
					this,
					ParserProvider.GetParser (textView.TextBuffer),
					XmlParserProvider,
					EditorLoggerService.CreateLogger<MSBuildCompletionSource>(textView))
			);
	}
}