// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	public struct PropertyWriteDiagnosticContext
	{
		readonly MSBuildAnalysisSession session;

		internal MSBuildDocument Document => session.Document;
		public CancellationToken CancellationToken => session.CancellationToken;

		public XElement Element { get; }
		public ValueInfo Info { get; }
		public MSBuildValueKind Kind { get; }
		internal ExpressionNode Node { get; }

		internal PropertyWriteDiagnosticContext (
			MSBuildAnalysisSession session, XElement element, ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			this.session = session;
			Element = element;
			Info = info;
			Kind = kind;
			Node = node;
		}

		public void ReportDiagnostic (MSBuildDiagnostic diagnostic) => session.ReportDiagnostic (diagnostic);
	}
}