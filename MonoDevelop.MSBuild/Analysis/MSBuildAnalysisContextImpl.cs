// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Analysis
{
	class MSBuildAnalysisContextImpl : MSBuildAnalysisContext
	{
		readonly Dictionary<MSBuildSyntaxKind, List<(MSBuildAnalyzer, Action<ElementDiagnosticContext>)>> elementActions
			= new Dictionary<MSBuildSyntaxKind, List<(MSBuildAnalyzer, Action<ElementDiagnosticContext>)>> ();

		readonly Dictionary<MSBuildSyntaxKind, List<(MSBuildAnalyzer, Action<AttributeDiagnosticContext>)>> attributeActions
			= new Dictionary<MSBuildSyntaxKind, List<(MSBuildAnalyzer, Action<AttributeDiagnosticContext>)>> ();

		readonly Dictionary<string, List<(MSBuildAnalyzer, Action<PropertyWriteDiagnosticContext>)>> propertyWriteActions
			= new Dictionary<string, List<(MSBuildAnalyzer, Action<PropertyWriteDiagnosticContext>)>> ();

		public override void RegisterElementAction (Action<ElementDiagnosticContext> action, ImmutableArray<MSBuildSyntaxKind> elementKinds)
		{
			foreach (var kind in elementKinds) {
				if (!kind.IsElementSyntax ()) {
					throw new ArgumentException ($"{kind} is not element syntax");
				}
				if (!elementActions.TryGetValue (kind, out var list)) {
					list = new List<(MSBuildAnalyzer, Action<ElementDiagnosticContext>)> ();
					elementActions.Add (kind, list);
				}
				list.Add ((currentAnalyzer, action));
			}
		}

		public override void RegisterAttributeAction (Action<AttributeDiagnosticContext> action, ImmutableArray<MSBuildSyntaxKind> attributeKinds)
		{
			foreach (var kind in attributeKinds) {
				if (!kind.IsAttributeSyntax ()) {
					throw new ArgumentException ($"{kind} is not attribute syntax");
				}
				if (!attributeActions.TryGetValue (kind, out var list)) {
					list = new List<(MSBuildAnalyzer, Action<AttributeDiagnosticContext>)> ();
					attributeActions.Add (kind, list);
				}
				list.Add ((currentAnalyzer, action));
			}
		}

		public override void RegisterPropertyWriteAction (Action<PropertyWriteDiagnosticContext> action, ImmutableArray<string> propertyNames)
		{
			foreach (var propertyName in propertyNames) {
				if (string.IsNullOrEmpty (propertyName)) {
					throw new ArgumentException ($"Null or empty value in {nameof(propertyNames)}");
				}
				if (!propertyWriteActions.TryGetValue (propertyName, out var list)) {
					list = new List<(MSBuildAnalyzer, Action<PropertyWriteDiagnosticContext>)> ();
					propertyWriteActions.Add (propertyName, list);
				}
				list.Add ((currentAnalyzer, action));
			}
		}

		public bool GetElementActions (MSBuildSyntaxKind kind, out List<(MSBuildAnalyzer, Action<ElementDiagnosticContext>)> actions) => elementActions.TryGetValue (kind, out actions);
		public bool GetAttributeActions (MSBuildSyntaxKind kind, out List<(MSBuildAnalyzer, Action<AttributeDiagnosticContext>)> actions) => attributeActions.TryGetValue (kind, out actions);
		internal bool GetPropertyWriteActions (string name, out List<(MSBuildAnalyzer, Action<PropertyWriteDiagnosticContext>)> actions) => propertyWriteActions.TryGetValue (name, out actions);

		MSBuildAnalyzer currentAnalyzer;

		public void RegisterAnalyzer (MSBuildAnalyzer analyzer)
		{
			currentAnalyzer = analyzer;
			try {
				analyzer.Initialize (this);
			} catch (Exception ex) {
				ReportAnalyzerError (analyzer, ex);
			}
			currentAnalyzer = null;
		}

		public void ReportAnalyzerError (MSBuildAnalyzer analyzer, Exception ex)
		{
			LoggingService.LogError ($"Failure in analyzer {analyzer.GetType ().FullName}, disabling", ex);
			analyzer.Disabled = true;
		}
	}
}
