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
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.CodeFixes
{

	[Export (typeof (MSBuildCodeActionProvider))]
	class ChangeMisspelledNameFixProvider : MSBuildCodeActionProvider
	{
		[ImportingConstructor]
		public ChangeMisspelledNameFixProvider ()
		{
		}

		public override ImmutableArray<string> FixableDiagnosticIds { get; } =
		[
			CoreDiagnostics.UnreadItem_Id,
			CoreDiagnostics.UnreadMetadata_Id,
			CoreDiagnostics.UnreadProperty_Id,
			CoreDiagnostics.UnwrittenItem_Id,
			CoreDiagnostics.UnwrittenMetadata_Id,
			CoreDiagnostics.UnwrittenProperty_Id,
			CoreDiagnostics.UnknownValue_Id,
			CoreDiagnostics.InvalidBool_Id
,
		];

		public override Task RegisterCodeActionsAsync (MSBuildCodeActionContext context)
		{
			foreach (var diag in context.GetMatchingDiagnosticsInSpan (FixableDiagnosticIds)) {
				var name = (string)diag.Properties[CoreDiagnosticProperty.MisspelledNameOrValue];

				TextSpan[] spans;
				if (diag.Properties.TryGetValue (CoreDiagnosticProperty.MisspelledNameSpans, out var spansObj)) {
					spans = (TextSpan[])spansObj;
				} else {
					spans = [diag.Span];
				}

				switch (diag.Descriptor.Id) {
				case CoreDiagnostics.UnreadItem_Id:
				case CoreDiagnostics.UnwrittenItem_Id:
					foreach (var item in MSBuildSpellChecker.FindSimilarItems (context.Document, name)) {
						context.RegisterCodeAction (new FixNameAction (spans, name, item.Name, context, diag));
					}
					break;

				case CoreDiagnostics.UnreadProperty_Id:
				case CoreDiagnostics.UnwrittenProperty_Id:
					// NOTE: don't fix writes with readonly properties
					bool includeReadOnlyProperties = diag.Descriptor.Id == CoreDiagnostics.UnreadProperty_Id;

					foreach (var prop in MSBuildSpellChecker.FindSimilarProperties (context.Document, name, includeReadOnlyProperties)) {
						context.RegisterCodeAction (new FixNameAction (spans, name, prop.Name, context, diag));
					}
					break;

				case CoreDiagnostics.UnreadMetadata_Id:
				case CoreDiagnostics.UnwrittenMetadata_Id:
					// NOTE: don't fix writes with reserved (i.e. readonly) metadata
					bool includeReservedMetadata = diag.Descriptor.Id == CoreDiagnostics.UnreadMetadata_Id;

					var itemName = (string)diag.Properties[CoreDiagnosticProperty.MisspelledMetadataItemName];
					foreach (var metadata in MSBuildSpellChecker.FindSimilarMetadata (context.Document, itemName, name, includeReservedMetadata)) {
						context.RegisterCodeAction (new FixNameAction (spans, name, metadata.Name, context, diag));
					}
					break;

				case CoreDiagnostics.UnknownValue_Id:
				case CoreDiagnostics.InvalidBool_Id:
					var expectedType = (ITypedSymbol)diag.Properties[CoreDiagnosticProperty.MisspelledValueExpectedType];

					foreach (var value in MSBuildSpellChecker.FindSimilarValues (context.Document, expectedType, name)) {
						context.RegisterCodeAction (new FixNameAction (spans, name, value.Name, context, diag));
					}
					break;
				}
			}

			return Task.CompletedTask;
		}
	}

	class FixNameAction (TextSpan[] spans, string oldName, string newName, MSBuildCodeActionContext context, MSBuildDiagnostic fixesDiagnostic) : MSBuildDocumentEditBuilderCodeAction (context)
	{
		public override string Title => $"Change '{oldName}' to '{newName}'";

		public override IReadOnlyList<MSBuildDiagnostic> FixesDiagnostics => [fixesDiagnostic];

		protected override void BuildEdit (MSBuildDocumentEditBuilder builder, CancellationToken cancellationToken)
		{
			foreach (var span in spans) {
				builder.Replace (span, newName);
			}
		}
	}
}
