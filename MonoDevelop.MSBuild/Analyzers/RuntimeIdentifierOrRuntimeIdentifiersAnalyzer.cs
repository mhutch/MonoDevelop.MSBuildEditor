// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Analyzers
{
	[MSBuildAnalyzer]
	class RuntimeIdentifierOrRuntimeIdentifiersAnalyzer : MSBuildAnalyzer
	{
		public const string UseRuntimeIdentifiersForMultipleRIDsDiagnosticId = nameof (UseRuntimeIdentifiersForMultipleRIDs);

		readonly MSBuildDiagnosticDescriptor UseRuntimeIdentifiersForMultipleRIDs = new MSBuildDiagnosticDescriptor (
			UseRuntimeIdentifiersForMultipleRIDsDiagnosticId,
			"Use RuntimeIdentifiers for multiple RIDs",
			"When targeting multiple RIDs, use the RuntimeIdentifiers property instead of RuntimeIdentifier",
			MSBuildDiagnosticSeverity.Error
		);

		public const string UseRuntimeIdentifierForSingleRIDDiagnosticId = nameof (UseRuntimeIdentifierForSingleRID);

		readonly MSBuildDiagnosticDescriptor UseRuntimeIdentifierForSingleRID = new MSBuildDiagnosticDescriptor (
			UseRuntimeIdentifierForSingleRIDDiagnosticId,
			"Use RuntimeIdentifier for single RID",
			"When targeting a single RID, use the RuntimeIdentifier property instead of RuntimeIdentifiers",
			MSBuildDiagnosticSeverity.Warning
		);

		public override ImmutableArray<MSBuildDiagnosticDescriptor> SupportedDiagnostics { get; }

		public RuntimeIdentifierOrRuntimeIdentifiersAnalyzer ()
		{
			SupportedDiagnostics = ImmutableArray.Create (
				UseRuntimeIdentifiersForMultipleRIDs, UseRuntimeIdentifierForSingleRID
			);
		}

		public override void Initialize (MSBuildAnalysisContext context)
		{
			context.RegisterPropertyWriteAction (AnalyzeRuntimeIdentifier, "RuntimeIdentifier");
			context.RegisterPropertyWriteAction (AnalyzeRuntimeIdentifiers, "RuntimeIdentifiers");
			context.RegisterCoreDiagnosticFilter (CoreDiagnosticFilter, CoreDiagnostics.UnexpectedList.Id);
		}

		void AnalyzeRuntimeIdentifier (PropertyWriteDiagnosticContext ctx)
		{
			// right now the visitor parses the expression with lists disabled because the type system says it doesn't expect lists, so we get a text node
			// however, once we attach parsed expressions into the AST they will likely have all options enabled and could be lists
			if (ctx.Node is ListExpression || (ctx.Node is ExpressionText t && t.Value.IndexOf (';') > -1)) {
				ctx.ReportDiagnostic (new MSBuildDiagnostic (UseRuntimeIdentifiersForMultipleRIDs, ctx.XElement.Span));
			}
		}

		void AnalyzeRuntimeIdentifiers (PropertyWriteDiagnosticContext ctx)
		{
			if (ctx.Node is ExpressionText t && t.Value.IndexOf (';') < 0) {
				ctx.ReportDiagnostic (new MSBuildDiagnostic (UseRuntimeIdentifierForSingleRID, ctx.XElement.Span));
			}
		}

		static bool CoreDiagnosticFilter (MSBuildDiagnostic arg) =>
			arg.Properties is not null
			&& arg.Properties.TryGetValue (CoreDiagnosticProperty.Symbol, out var value)
			&& value is PropertyInfo pi
			&& string.Equals (pi.Name, "RuntimeIdentifier", System.StringComparison.OrdinalIgnoreCase);
	}
}
