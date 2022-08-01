// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Editor.Analysis;

namespace MonoDevelop.MSBuild.Tests.Editor.Refactorings
{
	readonly record struct CodeFixesWithContext (List<MSBuildCodeFix> CodeFixes, ITextBuffer TextBuffer, ITextView? TextView = null);

	static class MSBuildRefactoringTestExtensions
	{
		public static async Task<CodeFixesWithContext> GetRefactorings (this MSBuildEditorTest test, MSBuildRefactoringService refactoringService, ITextView textView, CancellationToken cancellationToken = default)
		{
			// TODO: use a custom parser provider that constrains the analyzers
			var buffer = textView.TextBuffer;
			var parser = test.GetParser (buffer);

			var selection = textView.Selection.SelectedSpans.Single ();

			var parseResult = await parser.GetOrProcessAsync (buffer.CurrentSnapshot, cancellationToken);

			return new (
				await refactoringService.GetRefactorings (parseResult, selection, cancellationToken),
				buffer,
				textView
			);
		}


		public static Task<CodeFixesWithContext> GetRefactorings<T> (this MSBuildEditorTest test, string documentWithSelection, char selectionMarker = '$', CancellationToken cancellationToken = default)
			where T : MSBuildRefactoringProvider, new()
		{
			var refactoringService = new MSBuildRefactoringService (new[] { new T () });
			var textView = test.CreateTextViewWithSelection (documentWithSelection, selectionMarker);

			return test.GetRefactorings (refactoringService, textView, cancellationToken);
		}

		public static ITextView CreateTextViewWithSelection (this MSBuildEditorTest test, string documentWithSelection, char selectionMarker)
		{
			var parsed = TextWithMarkers.Parse (documentWithSelection, selectionMarker);
			var text = parsed.Text;
			var selection = parsed.GetMarkedSpan (selectionMarker);

			var textView = test.CreateTextView (text);
			var selectedSpan = new SnapshotSpan (textView.TextBuffer.CurrentSnapshot, selection.Start, selection.Length);
			textView.Selection.Select (selectedSpan, false);
			return textView;
		}
	}
}
