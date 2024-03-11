// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analyzers
{
	[MSBuildAnalyzer]
	class AppendNoWarnAnalyzer : MSBuildAnalyzer
	{
		public const string DiagnosticId = nameof (AppendNoWarn);

		readonly MSBuildDiagnosticDescriptor AppendNoWarn = new (
			DiagnosticId,
			"Append NoWarn values to existing value",
			"When settings the `NoWarn` property, you should append the additional values to the existing value of the property. " +
			"Otherwise, you may accidentally remove existing values.",
			MSBuildDiagnosticSeverity.Warning
		);

		const string NoWarnPropName = "NoWarn";

		public override ImmutableArray<MSBuildDiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create (AppendNoWarn);

		public override void Initialize (MSBuildAnalysisContext context)
		{
			context.RegisterPropertyWriteAction (AnalyzeProperty, NoWarnPropName);
		}

		void AnalyzeProperty (PropertyWriteDiagnosticContext ctx)
		{
			if (ctx.Node is ListExpression list) {
				foreach (var node in list.Nodes) {
					if (IsNoWarnPropertyRef (node)) {
						return;
					}
				}
			} else if (IsNoWarnPropertyRef (ctx.Node)) {
				return;
			}

			ctx.ReportDiagnostic (new MSBuildDiagnostic (SupportedDiagnostics[0], ctx.XElement.GetSquiggleSpan ()));

			static bool IsNoWarnPropertyRef (ExpressionNode node)
			{
				return TryGetTrimmedSingleNode (node) is ExpressionProperty prop && prop.IsSimpleProperty && string.Equals (prop.Name, NoWarnPropName, System.StringComparison.OrdinalIgnoreCase);
			}
		}

		static ExpressionNode? TryGetTrimmedSingleNode(ExpressionNode node)
		{
			if (node is not ConcatExpression concat) {
				return node;
			}

			int start = 0;
			if (concat.Nodes[start] is ExpressionText preText && string.IsNullOrWhiteSpace(preText.Value)) {
				start++;
			}

			int end = concat.Nodes.Count - 1;
			if (concat.Nodes[end] is ExpressionText postText && string.IsNullOrWhiteSpace (postText.Value)) {
				end--;
			}

			int count = start - end + 1;
			if (count == 1) {
				return concat.Nodes[start];
			}

			return null;
		}
	}
}