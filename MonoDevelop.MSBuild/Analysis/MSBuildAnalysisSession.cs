// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Language;

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

		public void ExecuteElementActions (MSBuildElement element)
		{
			if (Context.GetElementActions (element.SyntaxKind, out var actions)) {
				var ctx = new ElementDiagnosticContext (this, element);
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

		public void ExecuteAttributeActions (MSBuildAttribute attribute)
		{
			if (Context.GetAttributeActions (attribute.SyntaxKind, out var actions)) {
				var ctx = new AttributeDiagnosticContext (this, attribute);
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

		public void ExecutePropertyWriteActions (MSBuildPropertyElement element)
		{
			if (Context.GetPropertyWriteActions (element.ElementName, out var actions)) {
				var ctx = new PropertyWriteDiagnosticContext (this, element);
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
