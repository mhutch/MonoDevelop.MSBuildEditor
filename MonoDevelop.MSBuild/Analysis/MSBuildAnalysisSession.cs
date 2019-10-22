// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	class MSBuildAnalysisSession
	{
		public MSBuildAnalysisSession (MSBuildAnalysisContextImpl context, MSBuildDocument document, CancellationToken cancellationToken)
		{
			Context = context;
			Document = document;
			CancellationToken = cancellationToken;
		}

		public MSBuildAnalysisContextImpl Context { get; }
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }

		public List<MSBuildDiagnostic> Diagnostics { get; } = new List<MSBuildDiagnostic> ();

		public void ReportDiagnostic (MSBuildDiagnostic diagnostic) => Diagnostics.Add (diagnostic);

		public void ExecuteElementActions (XElement element, MSBuildElementSyntax resolved)
		{
			if (Context.GetElementActions (resolved?.SyntaxKind ?? MSBuildSyntaxKind.Unknown, out var actions)) {
				var ctx = new ElementDiagnosticContext (this, element, resolved);
				foreach (var (analyzer, action) in actions) {
					try {
						if (!analyzer.Disabled) {
							action (ctx);
						}
					} catch (Exception ex) {
						Context.ReportAnalyzerError (analyzer, ex);
					}
				}
			}
		}

		public void ExecuteAttributeActions (XElement element, XAttribute attribute, MSBuildElementSyntax resolvedElement, MSBuildAttributeSyntax resolvedAttribute)
		{
			if (Context.GetAttributeActions (resolvedAttribute?.SyntaxKind ?? MSBuildSyntaxKind.Unknown, out var actions)) {
				var ctx = new AttributeDiagnosticContext (this, element, attribute, resolvedElement, resolvedAttribute);
				foreach (var (analyzer, action) in actions) {
					try {
						if (!analyzer.Disabled) {
							action (ctx);
						}
					} catch (Exception ex) {
						Context.ReportAnalyzerError (analyzer, ex);
					}
				}
			}
		}

		public void ExecutePropertyWriteActions (XElement element, ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			if (Context.GetPropertyWriteActions (info.Name, out var actions)) {
				var ctx = new PropertyWriteDiagnosticContext (this, element, info, kind, node);
				foreach (var (analyzer, action) in actions) {
					try {
						if (!analyzer.Disabled) {
							action (ctx);
						}
					} catch (Exception ex) {
						Context.ReportAnalyzerError (analyzer, ex);
					}
				}
			}
		}

		public void AddCoreDiagnostics (IEnumerable<MSBuildDiagnostic> coreDiagnostics)
		{
			foreach (var diagnostic in coreDiagnostics) {
				bool addDiagnostic = true;
				if (Context.GetCoreDiagnosticFilters (diagnostic.Descriptor.Id, out var filters)) {
					foreach (var (analyzer, filter) in filters) {
						try {
							if (!analyzer.Disabled) {
								if (filter (diagnostic)) {
									addDiagnostic = false;
									continue;
								}
							}
						} catch (Exception ex) {
							Context.ReportAnalyzerError (analyzer, ex);
						}
					}
				}
				if (addDiagnostic) {
					Diagnostics.Add (diagnostic);
				}
			}
		}
	}
}
