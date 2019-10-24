// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using MonoDevelop.MSBuild.Analysis;
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
			// right now the visitor parses the expression with lists disabled because the type system says it doesn't expect lists, so we get a text node
			// however, once we attach parsed expressions into the AST they will likely have all options enabled and could be lists
			if (ctx.Node is ListExpression || (ctx.Node is ExpressionText t && t.Value.IndexOf (';') > -1)) {
				ctx.ReportDiagnostic (new MSBuildDiagnostic (UseTargetFrameworksForMultipleFrameworks, ctx.Element.Span));
			}
		}

		void AnalyzeTargetFrameworks (PropertyWriteDiagnosticContext ctx)
		{
			if (ctx.Node is ExpressionText t && t.Value.IndexOf (';') < 0) {
				ctx.ReportDiagnostic (new MSBuildDiagnostic (UseTargetFrameworkForSingleFramework, ctx.Element.Span));
			}
		}

		bool CoreDiagnosticFilter (MSBuildDiagnostic arg)
			=> arg.Properties != null && arg.Properties.TryGetValue ("Name", out var value) && (string)value == "TargetFramework";
	}
}
