// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Editor.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.CodeFixes
{
	[Export (typeof (MSBuildFixProvider))]
	class ChangeMisspelledNameFixProvider : MSBuildFixProvider
	{
		[ImportingConstructor]
		public ChangeMisspelledNameFixProvider (MSBuildSpellCheckerProvider spellCheckerProvider)
		{
			SpellCheckerProvider = spellCheckerProvider;
		}

		MSBuildSpellCheckerProvider SpellCheckerProvider { get; }

		public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create (
			CoreDiagnostics.UnreadItem_Id,
			CoreDiagnostics.UnreadMetadata_Id,
			CoreDiagnostics.UnreadProperty_Id,
			CoreDiagnostics.UnwrittenItem_Id,
			CoreDiagnostics.UnwrittenMetadata_Id,
			CoreDiagnostics.UnwrittenProperty_Id,
			CoreDiagnostics.UnknownValue_Id,
			CoreDiagnostics.InvalidBool_Id
		);

		public async override Task RegisterCodeFixesAsync (MSBuildFixContext context)
		{
			var spellChecker = SpellCheckerProvider.GetSpellChecker (context.Buffer);

			foreach (var diag in context.Diagnostics) {
				var name = (string)diag.Properties["Name"];

				TextSpan[] spans;
				if (diag.Properties.TryGetValue("Spans", out var spansObj)) {
					spans = (TextSpan[])spansObj;
				} else {
					spans = new[] { diag.Span };
				}

				switch (diag.Descriptor.Id) {
				case CoreDiagnostics.UnreadItem_Id:
				case CoreDiagnostics.UnwrittenItem_Id:
					foreach (var item in await spellChecker.FindSimilarItems (context.Document, name)) {
						context.RegisterCodeFix (new FixNameAction (spans, name, item.Name), diag);
					}
					break;

				case CoreDiagnostics.UnreadProperty_Id:
				case CoreDiagnostics.UnwrittenProperty_Id:
					foreach (var prop in await spellChecker.FindSimilarProperties (context.Document, name)) {
						// don't fix writes with reserved properties
						if (prop.Reserved && diag.Descriptor.Id == CoreDiagnostics.UnreadMetadata.Id) {
							continue;
						}
						context.RegisterCodeFix (new FixNameAction (spans, name, prop.Name), diag);
					}
					break;

				case CoreDiagnostics.UnreadMetadata_Id:
				case CoreDiagnostics.UnwrittenMetadata_Id:
					var itemName = (string)diag.Properties["ItemName"];
					foreach (var metadata in await spellChecker.FindSimilarMetadata (context.Document, itemName, name)) {
						// don't fix writes with reserved metadata
						if (metadata.Reserved && diag.Descriptor.Id == CoreDiagnostics.UnreadMetadata.Id) {
							continue;
						}
						context.RegisterCodeFix (new FixNameAction (spans, name, metadata.Name), diag);
					}
					break;

				case CoreDiagnostics.UnknownValue_Id:
				case CoreDiagnostics.InvalidBool_Id:
					var kind = (MSBuildValueKind)diag.Properties["ValueKind"];
					CustomTypeInfo customType = kind == MSBuildValueKind.CustomType? (CustomTypeInfo)diag.Properties["CustomType"] : null;
					foreach (var value in await spellChecker.FindSimilarValues (context.Document, kind, customType, name)) {
						context.RegisterCodeFix (new FixNameAction (spans, name, value.Name), diag);
					}
					break;
				}
			}
		}

		class FixNameAction : SimpleMSBuildCodeAction
		{
			readonly TextSpan[] spans;
			readonly string oldName, newName;

			public FixNameAction (TextSpan[] spans, string oldName, string newName)
			{
				this.spans = spans;
				this.newName = newName;
				this.oldName = oldName;
			}

			public override string Title => $"Change '{oldName}' to '{newName}'";

			protected override MSBuildCodeActionOperation CreateOperation ()
			{
				var op = new EditTextActionOperation ();
				foreach (var span in spans) {
					op.Replace (span, newName);
				}
				return op;
			}
		}
	}
}
