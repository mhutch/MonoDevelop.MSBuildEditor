// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Editor.Parsing;

namespace MonoDevelop.MSBuild.Editor.TextStructure
{
	[Export (typeof (ITextStructureNavigatorProvider))]
	[ContentType (MSBuildContentType.Name)]
	class MSBuildTextStructureNavigatorProvider : ITextStructureNavigatorProvider
	{
		[ImportingConstructor]
		public MSBuildTextStructureNavigatorProvider (
			ITextStructureNavigatorSelectorService navigatorService,
			IContentTypeRegistryService contentTypeRegistry,
			XmlParserProvider xmlParserProvider,
			IEditorLoggerFactory loggerFactory)
		{
			NavigatorService = navigatorService;
			ContentTypeRegistry = contentTypeRegistry;
			XmlParserProvider = xmlParserProvider;
			LoggerFactory = loggerFactory;
		}

		public ITextStructureNavigatorSelectorService NavigatorService { get; }
		public IContentTypeRegistryService ContentTypeRegistry { get; }
		public XmlParserProvider XmlParserProvider { get; }
		public IEditorLoggerFactory LoggerFactory { get; }

		public ITextStructureNavigator CreateTextStructureNavigator (ITextBuffer textBuffer)
		{
			return new MSBuildTextStructureNavigator (textBuffer, this);
		}
	}
}
