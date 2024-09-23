// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Analyzers;
using MonoDevelop.MSBuild.Editor.CodeActions;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.CodeFixes
{
	[Export (typeof (MSBuildCodeActionProvider))]
	class AppendNoWarnFixProvider : MSBuildCodeActionProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } = [AppendNoWarnAnalyzer.DiagnosticId];

		public override Task RegisterCodeActionsAsync (MSBuildCodeActionContext context)
		{
			foreach (var diag in context.GetMatchingDiagnosticsInSpan(FixableDiagnosticIds)) {
				if (context.XDocument.FindAtOffset (diag.Span.Start) is XElement prop && prop.InnerSpan is TextSpan valueSpan) {
					context.RegisterCodeAction (new PrependListValueAction (valueSpan, "$(NoWarn)", context, diag));
				}
			}
			return Task.CompletedTask;
		}

		class PrependListValueAction (TextSpan valueSpan, string valueToPrepend, MSBuildCodeActionContext context, MSBuildDiagnostic fixesDiagnostic) : MSBuildDocumentEditBuilderCodeAction(context)
		{
			public override string Title => $"Prepend '{valueToPrepend}' to list";

			public override IReadOnlyList<MSBuildDiagnostic> FixesDiagnostics => [ fixesDiagnostic ];

			protected override void BuildEdit (MSBuildDocumentEditBuilder builder, CancellationToken cancellationToken) => builder.Insert (valueSpan.Start, valueToPrepend + ";");
		}
	}
}
