// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Editor.Logging;

using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuild.Editor.QuickInfo
{
	[Export (typeof (IAsyncQuickInfoSourceProvider))]
	[Name (ProviderName)]
	[ContentType (MSBuildContentType.Name)]
	[Order]
	class MSBuildQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
	{
		public const string ProviderName = "MSBuild Quick Info Provider";

		[ImportingConstructor]
		public MSBuildQuickInfoSourceProvider (
			IFunctionTypeProvider functionTypeProvider,
			IPackageSearchManager packageSearchManager,
			DisplayElementFactory displayElementFactory,
			MSBuildParserProvider parserProvider,
			IEditorLoggerFactory loggerFactory)
		{
			FunctionTypeProvider = functionTypeProvider;
			PackageSearchManager = packageSearchManager;
			DisplayElementFactory = displayElementFactory;
			ParserProvider = parserProvider;
			LoggerFactory = loggerFactory;
		}

		public IFunctionTypeProvider FunctionTypeProvider { get; }
		public IPackageSearchManager PackageSearchManager { get; }
		public DisplayElementFactory DisplayElementFactory { get; }
		public MSBuildParserProvider ParserProvider { get; }
		public IEditorLoggerFactory LoggerFactory { get; }

		public IAsyncQuickInfoSource TryCreateQuickInfoSource (ITextBuffer textBuffer)
			=> textBuffer.Properties.GetOrCreateSingletonProperty (() => {
				var logger = LoggerFactory.CreateLogger<MSBuildQuickInfoSource> (textBuffer);
				return new MSBuildQuickInfoSource (textBuffer, logger, this);
			});
	}
}