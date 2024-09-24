// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
	class FixMultitargetingPluralizationFixProvider : MSBuildCodeActionProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; }
			= [
				TargetFrameworksOrTargetFrameworkAnalyzer.UseTargetFrameworksForMultipleFrameworksDiagnosticId,
				TargetFrameworksOrTargetFrameworkAnalyzer.UseTargetFrameworkForSingleFrameworkDiagnosticId,
				RuntimeIdentifierOrRuntimeIdentifiersAnalyzer.UseRuntimeIdentifiersForMultipleRIDsDiagnosticId,
				RuntimeIdentifierOrRuntimeIdentifiersAnalyzer.UseRuntimeIdentifierForSingleRIDDiagnosticId
			];

		public override Task RegisterCodeActionsAsync (MSBuildCodeActionContext context)
		{
			foreach (var diag in context.GetMatchingDiagnosticsInSpan (FixableDiagnosticIds)) {
				var prop = context.XDocument.FindAtOffset (diag.Span.Start) as XElement;
				if (prop == null || prop.ClosingTag == null || prop.IsSelfClosing) {
					//FIXME log error?
					continue;
				}

				string newName = diag.Descriptor.Id switch
				{
					TargetFrameworksOrTargetFrameworkAnalyzer.UseTargetFrameworksForMultipleFrameworksDiagnosticId => "TargetFrameworks",
					TargetFrameworksOrTargetFrameworkAnalyzer.UseTargetFrameworkForSingleFrameworkDiagnosticId => "TargetFramework",
					RuntimeIdentifierOrRuntimeIdentifiersAnalyzer.UseRuntimeIdentifiersForMultipleRIDsDiagnosticId => "RuntimeIdentifiers",
					RuntimeIdentifierOrRuntimeIdentifiersAnalyzer.UseRuntimeIdentifierForSingleRIDDiagnosticId => "RuntimeIdentifier",
					_ => throw new InvalidOperationException ()
				};

				context.RegisterCodeAction (new ChangePropertyNameAction (prop, newName, context, diag));
			}

			return Task.CompletedTask;
		}

		class ChangePropertyNameAction (XElement prop, string newName, MSBuildCodeActionContext context, MSBuildDiagnostic fixesDiagnostic) : MSBuildDocumentEditBuilderCodeAction(context)
		{
			public override string Title => $"Change '{prop.Name}' to '{newName}'";

			public override IReadOnlyList<MSBuildDiagnostic> FixesDiagnostics => [ fixesDiagnostic ];

			protected override void BuildEdit (MSBuildDocumentEditBuilder builder, CancellationToken cancellationToken) => builder.RenameElement (prop, newName);
		}
	}
}