// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Editor.Completion;
using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	[Export (typeof (IAsyncCompletionSourceProvider))]
	[Name ("MSBuild Completion Source Provider")]
	[ContentType (MSBuildContentType.Name)]
	class MSBuildCompletionSourceProvider : IAsyncCompletionSourceProvider
	{
		[ImportingConstructor]
		public MSBuildCompletionSourceProvider (
			IFunctionTypeProvider functionTypeProvider,
			IPackageSearchManager packageSearchManager,
			DisplayElementFactory displayElementFactory,
			JoinableTaskContext joinableTaskContext,
			MSBuildParserProvider parserProvider,
			XmlParserProvider xmlParserProvider,
			ILogger<MSBuildCompletionSource> logger)
		{
			FunctionTypeProvider = functionTypeProvider;
			PackageSearchManager = packageSearchManager;
			DisplayElementFactory = displayElementFactory;
			JoinableTaskContext = joinableTaskContext;
			ParserProvider = parserProvider;
			XmlParserProvider = xmlParserProvider;
			Logger = logger;
		}

		public IFunctionTypeProvider FunctionTypeProvider { get; }
		public IPackageSearchManager PackageSearchManager { get; }
		public DisplayElementFactory DisplayElementFactory { get; }
		public JoinableTaskContext JoinableTaskContext { get;  }
		public MSBuildParserProvider ParserProvider { get; }
		public ILogger Logger { get; }
		public XmlParserProvider XmlParserProvider { get; }

		public IAsyncCompletionSource GetOrCreate (ITextView textView) =>
			textView.Properties.GetOrCreateSingletonProperty (
				typeof (MSBuildCompletionSource),
				() => new MSBuildCompletionSource (textView, this)
			);
	}
}