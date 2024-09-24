// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Editor.CodeActions;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.CodeFixes
{
	[Export (typeof (MSBuildCodeActionProvider))]
	class RemovePropertyOrMetadataWithDefaultValueFixProvider : MSBuildCodeActionProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } = [CoreDiagnostics.HasDefaultValue.Id];

		public override Task RegisterCodeActionsAsync (MSBuildCodeActionContext context)
		{
			foreach (var diag in context.GetMatchingDiagnosticsInSpan (FixableDiagnosticIds)) {
				if (!(diag.Properties.TryGetValue (CoreDiagnosticProperty.Symbol, out var val) && val is VariableInfo info)) {
					continue;
				}

				switch (context.XDocument.FindAtOffset (diag.Span.Start)) {
				case XElement el:
					context.RegisterCodeAction (new RemoveRedundantElementAction (el, DescriptionFormatter.GetKindNoun (info), context, diag));
					break;
				case XAttribute att:
					context.RegisterCodeAction (new RemoveRedundantAttributeAction (att, DescriptionFormatter.GetKindNoun (info), context, diag));
					break;
				}
			}
			return Task.CompletedTask;
		}

		class RemoveRedundantElementAction(XElement element, string kindNoun, MSBuildCodeActionContext context, MSBuildDiagnostic fixesDiagnostic) : MSBuildDocumentEditBuilderCodeAction(context)
		{
			public override string Title => $"Remove redundant {kindNoun} '{element.Name}'";

			public override IReadOnlyList<MSBuildDiagnostic> FixesDiagnostics => [ fixesDiagnostic ];

			protected override void BuildEdit (MSBuildDocumentEditBuilder builder, CancellationToken cancellationToken) => builder.RemoveElement (element);
		}

		class RemoveRedundantAttributeAction(XAttribute attribute, string kindNoun, MSBuildCodeActionContext context, MSBuildDiagnostic fixesDiagnostic) : MSBuildDocumentEditBuilderCodeAction(context)
		{
			public override string Title => $"Remove redundant {kindNoun} '{attribute.Name}'";

			public override IReadOnlyList<MSBuildDiagnostic> FixesDiagnostics => [ fixesDiagnostic ];

			protected override void BuildEdit (MSBuildDocumentEditBuilder builder, CancellationToken cancellationToken) => builder.RemoveAttribute (attribute);
		}
	}
}
