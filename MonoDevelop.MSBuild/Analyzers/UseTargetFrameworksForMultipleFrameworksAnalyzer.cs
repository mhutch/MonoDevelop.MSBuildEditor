// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;

namespace MonoDevelop.MSBuild.Analyzers
{
	[MSBuildAnalyzer]
	class UseTargetFrameworksForMultipleFrameworksAnalyzer : MSBuildAnalyzer
	{
		public override ImmutableArray<MSBuildDiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create (
			new MSBuildDiagnosticDescriptor (
				"UseTargetFrameworksForMultipleFrameworks",
				"Use TargetFrameworks",
				"When targeting multiple frameworks, use the TargetFrameworks property instead of TargetFramework",
				MSBuildDiagnosticSeverity.Error
			)
		);

		public override void Initialize (MSBuildAnalysisContext context)
		{
			context.RegisterPropertyWriteAction (AnalyzeProperty, "TargetFramework");
			context.RegisterCoreDiagnosticFilter (CoreDiagnosticFilter, CoreDiagnostics.UnexpectedList.Id);
		}

		void AnalyzeProperty (PropertyWriteDiagnosticContext ctx)
		{
			// right now the visitor parses the expression with lists disabled because the type system says it doesn't expect lists, so we get a text node
			// however, once we attach parsed expressions into the AST they will likely have all options enabled and could be lists
			if (ctx.Node is ListExpression || (ctx.Node is ExpressionText t && t.Value.IndexOf (';') > -1)) {
				ctx.ReportDiagnostic (new MSBuildDiagnostic (SupportedDiagnostics[0], ctx.Element.Span));
			}
		}

		bool CoreDiagnosticFilter (MSBuildDiagnostic arg)
			=> arg.Properties != null && arg.Properties.TryGetValue ("Name", out var value) && (string)value == "TargetFramework";
	}
}
