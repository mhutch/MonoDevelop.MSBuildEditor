// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Editor.Completion;

namespace MonoDevelop.MSBuild.Editor.TextStructure
{
	[Export (typeof (ITextStructureNavigatorProvider))]
	[ContentType (MSBuildContentType.Name)]
	class MSBuildTextStructureNavigatorProvider : ITextStructureNavigatorProvider
	{
		readonly ITextStructureNavigatorSelectorService navigatorService;
		readonly IContentTypeRegistryService contentTypeRegistry;
		readonly XmlParserProvider xmlParserProvider;

		[ImportingConstructor]
		public MSBuildTextStructureNavigatorProvider (
			ITextStructureNavigatorSelectorService navigatorService,
			IContentTypeRegistryService contentTypeRegistry,
			XmlParserProvider xmlParserProvider)
		{
			this.navigatorService = navigatorService;
			this.contentTypeRegistry = contentTypeRegistry;
			this.xmlParserProvider = xmlParserProvider;
		}

		public ITextStructureNavigator CreateTextStructureNavigator (ITextBuffer textBuffer)
		{
			var codeNavigator = navigatorService.CreateTextStructureNavigator (
				textBuffer,
				contentTypeRegistry.GetContentType (XmlContentTypeNames.XmlCore)
			);
			return new MSBuildTextStructureNavigator (textBuffer, codeNavigator, xmlParserProvider);
		}
	}
}
