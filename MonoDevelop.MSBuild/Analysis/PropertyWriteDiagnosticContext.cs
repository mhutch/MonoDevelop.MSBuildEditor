// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	public readonly struct PropertyWriteDiagnosticContext
	{
		readonly MSBuildAnalysisSession session;

		internal MSBuildDocument Document => session.Document;
		public CancellationToken CancellationToken => session.CancellationToken;

		public MSBuildPropertyElement Element { get; }
		public XElement XElement => Element.XElement;
		public MSBuildValueKind Kind => Element.Syntax.ValueKind;
		internal ExpressionNode Node => Element.Value;

		internal PropertyWriteDiagnosticContext (
			MSBuildAnalysisSession session, MSBuildPropertyElement element)
		{
			this.session = session;
			Element = element;
		}

		public void ReportDiagnostic (MSBuildDiagnostic diagnostic) => session.ReportDiagnostic (diagnostic);
	}
}