// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

using MonoDevelop.MSBuild.Dom;
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

		public MSBuildAttribute Attribute { get; }
		public MSBuildElement Element => Attribute.Parent;
		public XElement XElement => Element.XElement;
		public XAttribute XAttribute => Attribute.XAttribute;
		public MSBuildElementSyntax ElementSyntax => Element.Syntax;
		public MSBuildAttributeSyntax AttributeSyntax => Attribute.Syntax;
		public MSBuildSyntaxKind ElementSyntaxKind => ElementSyntax.SyntaxKind;
		public MSBuildSyntaxKind AttributeSyntaxKind => AttributeSyntax.SyntaxKind;
		public MSBuildValueKind ValueKind => AttributeSyntax.ValueKind;

		internal AttributeDiagnosticContext (
			MSBuildAnalysisSession session,
			MSBuildAttribute attribute)
		{
			this.session = session;
			Attribute = attribute;
		}

		public void ReportDiagnostic (MSBuildDiagnostic diagnostic) => session.ReportDiagnostic (diagnostic);
	}
}