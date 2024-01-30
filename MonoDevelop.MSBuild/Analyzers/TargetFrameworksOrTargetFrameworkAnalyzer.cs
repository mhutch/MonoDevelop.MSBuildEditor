// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Linq;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;

namespace MonoDevelop.MSBuild.Analyzers
{
	[MSBuildAnalyzer]
	class TargetFrameworksOrTargetFrameworkAnalyzer : MSBuildAnalyzer
	{
		public const string UseTargetFrameworksForMultipleFrameworksDiagnosticId = nameof (UseTargetFrameworksForMultipleFrameworks);

		readonly MSBuildDiagnosticDescriptor UseTargetFrameworksForMultipleFrameworks = new MSBuildDiagnosticDescriptor (
			UseTargetFrameworksForMultipleFrameworksDiagnosticId,
			"Use TargetFrameworks for multiple frameworks",
			"When targeting multiple frameworks, use the TargetFrameworks property instead of TargetFramework",
			MSBuildDiagnosticSeverity.Error
		);

		public const string UseTargetFrameworkForSingleFrameworkDiagnosticId = nameof (UseTargetFrameworkForSingleFramework);

		readonly MSBuildDiagnosticDescriptor UseTargetFrameworkForSingleFramework = new MSBuildDiagnosticDescriptor (
			UseTargetFrameworkForSingleFrameworkDiagnosticId,
			"Use TargetFramework for single framework",
			"When targeting a single framework, use the TargetFramework property instead of TargetFrameworks",
			MSBuildDiagnosticSeverity.Warning
		);

		public override ImmutableArray<MSBuildDiagnosticDescriptor> SupportedDiagnostics { get; }

		public TargetFrameworksOrTargetFrameworkAnalyzer ()
		{
			SupportedDiagnostics = ImmutableArray.Create (
				UseTargetFrameworksForMultipleFrameworks, UseTargetFrameworkForSingleFramework
			);
		}

		public override void Initialize (MSBuildAnalysisContext context)
		{
			context.RegisterPropertyWriteAction (AnalyzeTargetFramework, "TargetFramework");
			context.RegisterPropertyWriteAction (AnalyzeTargetFrameworks, "TargetFrameworks");
			context.RegisterCoreDiagnosticFilter (CoreDiagnosticFilter, CoreDiagnostics.UnexpectedList.Id);
		}

		void AnalyzeTargetFramework (PropertyWriteDiagnosticContext ctx)
		{
			if (ctx.Node is ListExpression) {
				ctx.ReportDiagnostic (new MSBuildDiagnostic (UseTargetFrameworksForMultipleFrameworks, ctx.XElement.Span));
			}
		}

		void AnalyzeTargetFrameworks (PropertyWriteDiagnosticContext ctx)
		{
			if (ctx.Node is ExpressionText t && t.Value.IndexOf (';') < 0) {
				// if there's another TargetFrameworks element in the document that doesn't have a single framework, it's fine, they're probably doing conditional stuff
				if (ctx.Document.ProjectElement.GetElements<MSBuildPropertyGroupElement> ().Any (HasTargetFrameworksPropertyWithNonSingularValue)) {
					return;
				}
				ctx.ReportDiagnostic (new MSBuildDiagnostic (UseTargetFrameworkForSingleFramework, ctx.XElement.Span));
			}
		}

		static bool HasTargetFrameworksPropertyWithNonSingularValue (MSBuildPropertyGroupElement pg) =>
			pg.PropertyElements.Any (p => p.IsElementNamed ("TargetFrameworks") && p.Value is not ExpressionText t);

		bool CoreDiagnosticFilter (MSBuildDiagnostic arg)
			=> arg.Properties != null && arg.Properties.TryGetValue ("Name", out var value) && (string)value == "TargetFramework";
	}
}
