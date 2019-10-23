// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	public readonly struct AttributeDiagnosticContext
	{
		readonly MSBuildAnalysisSession session;
		internal MSBuildDocument Document => session.Document;
		public CancellationToken CancellationToken => session.CancellationToken;

		public XElement Element { get; }
		public XAttribute Attribute { get; }
		public MSBuildElementSyntax ElementSyntax { get; }
		public MSBuildAttributeSyntax AttributeSyntax { get; }
		public MSBuildSyntaxKind ElementSyntaxKind => ElementSyntax?.SyntaxKind ?? MSBuildSyntaxKind.Unknown;
		public MSBuildSyntaxKind AttributeSyntaxKind => AttributeSyntax?.SyntaxKind ?? MSBuildSyntaxKind.Unknown;
		public MSBuildValueKind ValueKind => AttributeSyntax?.ValueKind ?? MSBuildValueKind.Unknown;

		internal AttributeDiagnosticContext (
			MSBuildAnalysisSession session,
			XElement element,
			XAttribute attribute,
			MSBuildElementSyntax elementSyntax,
			MSBuildAttributeSyntax attributeSyntax)
		{
			this.session = session;
			Element = element;
			Attribute = attribute;
			ElementSyntax = elementSyntax;
			AttributeSyntax = attributeSyntax;
		}

		public void ReportDiagnostic (MSBuildDiagnostic diagnostic) => session.ReportDiagnostic (diagnostic);
	}
}