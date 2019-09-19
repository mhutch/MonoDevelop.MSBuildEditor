// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class MSBuildParseResult : XmlParseResult
	{
		public MSBuildParseResult (MSBuildRootDocument msbuildDocument, XDocument xDocument, List<XmlDiagnosticInfo> diagnostics, ITextSnapshot snapshot)
			: base (xDocument, diagnostics, snapshot)
		{
			MSBuildDocument = msbuildDocument;
		}

		public MSBuildRootDocument MSBuildDocument { get; }
	}
}