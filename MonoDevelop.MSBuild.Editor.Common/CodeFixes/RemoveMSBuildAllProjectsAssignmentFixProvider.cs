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
	class RemoveMSBuildAllProjectsAssignmentFixProvider : MSBuildCodeActionProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; }
			= [DoNotAssignMSBuildAllProjectsAnalyzer.DiagnosticId];

		public override Task RegisterCodeActionsAsync (MSBuildCodeActionContext context)
		{
			foreach (var diag in context.GetMatchingDiagnosticsInSpan (FixableDiagnosticIds)) {
				if (context.XDocument.FindAtOffset (diag.Span.Start) is XElement el) {
					context.RegisterCodeAction (new RemoveMSBuildAllProjectsAssignmentAction (el, context, diag));
				}
			}
			return Task.CompletedTask;
		}

		class RemoveMSBuildAllProjectsAssignmentAction(XElement element, MSBuildCodeActionContext context, MSBuildDiagnostic fixesDiagnostic) : MSBuildDocumentEditBuilderCodeAction(context)
		{
			public override string Title => $"Remove redundant assignment of 'MSBuildAllProjectsAssignment'";

			public override IReadOnlyList<MSBuildDiagnostic> FixesDiagnostics => [ fixesDiagnostic ];

			protected override void BuildEdit (MSBuildDocumentEditBuilder builder, CancellationToken cancellationToken) => builder.RemoveElement (element);
		}
	}
}
