// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Language.Syntax;

namespace MonoDevelop.MSBuild.Analysis
{
	partial class MSBuildAnalysisContextImpl : MSBuildAnalysisContext
	{
		readonly Dictionary<MSBuildSyntaxKind, List<(MSBuildAnalyzer, Action<ElementDiagnosticContext>)>> elementActions = new();

		readonly Dictionary<MSBuildSyntaxKind, List<(MSBuildAnalyzer, Action<AttributeDiagnosticContext>)>> attributeActions = new();

		readonly Dictionary<string, List<(MSBuildAnalyzer, Action<PropertyWriteDiagnosticContext>)>> propertyWriteActions = new();

		readonly Dictionary<string, List<(MSBuildAnalyzer, Func<MSBuildDiagnostic, bool>)>> coreDiagnosticFilters = new();
		private readonly ILogger logger;

		public MSBuildAnalysisContextImpl (ILogger logger)
		{
			this.logger = logger;
		}

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

		public override void RegisterCoreDiagnosticFilter (Func<MSBuildDiagnostic, bool> filter, ImmutableArray<string> diagnosticIds)
		{
			foreach (var diagnosticId in diagnosticIds) {
				if (string.IsNullOrEmpty (diagnosticId)) {
					throw new ArgumentException ($"Null or empty value in {nameof (diagnosticIds)}");
				}
				if (!coreDiagnosticFilters.TryGetValue (diagnosticId, out var list)) {
					list = new List<(MSBuildAnalyzer, Func<MSBuildDiagnostic, bool>)> ();
					coreDiagnosticFilters.Add (diagnosticId, list);
				}
				list.Add ((currentAnalyzer, filter));
			}
		}

		public bool GetElementActions (MSBuildSyntaxKind kind, out List<(MSBuildAnalyzer, Action<ElementDiagnosticContext>)> actions) => elementActions.TryGetValue (kind, out actions);
		public bool GetAttributeActions (MSBuildSyntaxKind kind, out List<(MSBuildAnalyzer, Action<AttributeDiagnosticContext>)> actions) => attributeActions.TryGetValue (kind, out actions);
		internal bool GetPropertyWriteActions (string name, out List<(MSBuildAnalyzer, Action<PropertyWriteDiagnosticContext>)> actions) => propertyWriteActions.TryGetValue (name, out actions);
		internal bool GetCoreDiagnosticFilters (string diagnosticId, out List<(MSBuildAnalyzer, Func<MSBuildDiagnostic, bool>)> filters) => coreDiagnosticFilters.TryGetValue (diagnosticId, out filters);

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
			LogAnalyzerError(logger, ex, analyzer.GetType ().FullName);
			analyzer.Disabled = true;
		}

		[LoggerMessage (Level = LogLevel.Error, Message = "Failure in analyzer {analyzer}, disabling")]
		static partial void LogAnalyzerError (ILogger logger, Exception ex, string analyzer);
	}
}
