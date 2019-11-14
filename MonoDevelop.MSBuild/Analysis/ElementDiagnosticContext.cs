// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	public readonly struct ElementDiagnosticContext
	{
		readonly MSBuildAnalysisSession session;
		internal MSBuildDocument Document => session.Document;
		public CancellationToken CancellationToken => session.CancellationToken;

		public MSBuildElement Element { get; }
		public XElement XElement => Element.XElement;
		public MSBuildElementSyntax ElementSyntax => Element.Syntax;
		public MSBuildSyntaxKind SyntaxKind => Element.SyntaxKind;
		public MSBuildValueKind ValueKind => ElementSyntax.ValueKind;

		internal ElementDiagnosticContext (
			MSBuildAnalysisSession session,
			MSBuildElement element)
		{
			this.session = session;
			Element = element;
		}

		public void ReportDiagnostic (MSBuildDiagnostic diagnostic) => session.ReportDiagnostic (diagnostic);
	}
}