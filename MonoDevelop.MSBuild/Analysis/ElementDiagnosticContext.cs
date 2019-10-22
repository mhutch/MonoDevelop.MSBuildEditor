// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	public struct ElementDiagnosticContext
	{
		readonly MSBuildAnalysisSession session;
		internal MSBuildDocument Document => session.Document;
		public CancellationToken CancellationToken => session.CancellationToken;

		public XElement Element { get; }
		public MSBuildElementSyntax ElementSyntax { get; }
		public MSBuildSyntaxKind SyntaxKind => ElementSyntax?.SyntaxKind ?? MSBuildSyntaxKind.Unknown;
		public MSBuildValueKind ValueKind => ElementSyntax?.ValueKind ?? MSBuildValueKind.Unknown;

		internal ElementDiagnosticContext (
			MSBuildAnalysisSession session,
			XElement element,
			MSBuildElementSyntax elementSyntax)
		{
			this.session = session;
			Element = element;
			ElementSyntax = elementSyntax;
		}

		public void ReportDiagnostic (MSBuildDiagnostic diagnostic) => session.ReportDiagnostic (diagnostic);
	}
}