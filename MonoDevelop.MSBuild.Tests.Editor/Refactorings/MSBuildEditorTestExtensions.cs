// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Editor;
using MonoDevelop.MSBuild.Editor.Analysis;
using MonoDevelop.MSBuild.Editor.CodeActions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Editor.Tests.Extensions;
using MonoDevelop.Xml.Tests;
using MonoDevelop.Xml.Tests.Utils;

using NUnit.Framework;

using TextSpan = MonoDevelop.Xml.Dom.TextSpan;

namespace MonoDevelop.MSBuild.Tests.Editor
{
	readonly record struct CodeActionsWithContext (List<MSBuildCodeAction>? CodeActions, ITextBuffer TextBuffer, ITextView TextView);

	static class MSBuildEditorTestExtensions
	{
		public static async Task<CodeActionsWithContext> GetRefactorings (this MSBuildEditorTest test, MSBuildCodeActionService codeActionService, ITextView textView, CancellationToken cancellationToken = default)
		{
			// TODO: use a custom parser provider that constrains the analyzers
			var buffer = textView.TextBuffer;
			var parser = test.GetParser (buffer);

			var selection = textView.Selection.SelectedSpans.Single ();

			var parseResult = await parser.GetOrProcessAsync (buffer.CurrentSnapshot, cancellationToken);

			var sourceText = Microsoft.CodeAnalysis.Text.Extensions.AsText (buffer.CurrentSnapshot);
			var options = new VSEditorOptionsReader (textView.Options);

			return new (
				await codeActionService.GetCodeActions (sourceText, parseResult.MSBuildDocument, new TextSpan (selection.Start, selection.Length), [], options, cancellationToken),
				buffer,
				textView
			);
		}

		public static async Task<CodeActionsWithContext> GetRefactorings<T> (this MSBuildEditorTest test, string documentWithSelection, char selectionMarker = '|', CancellationToken cancellationToken = default)
			where T : MSBuildCodeActionProvider, new()
		{
			var refactoringService = new MSBuildCodeActionService (new[] { new T () });
			var textView = test.CreateTextViewWithSelection (documentWithSelection, selectionMarker, allowZeroWidthSingleMarker: true);

			return await test.GetRefactorings (refactoringService, textView, cancellationToken);
		}

		public static ITextView CreateTextViewWithSelection (this MSBuildEditorTest test, string documentWithSelection, char selectionMarker, bool allowZeroWidthSingleMarker = false)
		{
			var parsed = TextWithMarkers.Parse (documentWithSelection, selectionMarker);

			var text = parsed.Text;
			TextSpan selection = parsed.GetMarkedSpan (selectionMarker, allowZeroWidthSingleMarker);

			var textView = test.CreateTextView (text, "foo.csproj");
			SetDefaultEditorOptions (textView);

			textView.Caret.MoveTo (new SnapshotPoint (textView.TextBuffer.CurrentSnapshot, selection.End));

			var selectedSpan = new SnapshotSpan (textView.TextBuffer.CurrentSnapshot, selection.Start, selection.Length);
			textView.Selection.Select (selectedSpan, false);

			return textView;
		}

		public static ITextView CreateTextViewWithCaret (this MSBuildEditorTest test, string documentWithSelection, char caretMarker)
		{
			var parsed = TextWithMarkers.Parse (documentWithSelection, caretMarker);
			var text = parsed.Text;
			var position = parsed.GetMarkedPosition (caretMarker);

			var textView = test.CreateTextView (text);

			return textView;
		}

		public static async Task TestRefactoring<T> (
			this MSBuildEditorTest test,
			string documentWithSelection,
			string invokeFixWithTitle,
			int expectedFixCount,
			string expectedTextAfterInvoke,
			string? typeText = null,
			string? expectedTextAfterTyping = null,
			char selectionMarker = '|',
			CancellationToken cancellationToken = default
			) where T : MSBuildCodeActionProvider, new()
		{
			await test.Catalog.JoinableTaskContext.Factory.SwitchToMainThreadAsync ();
			var ctx = await test.GetRefactorings<T> (documentWithSelection, selectionMarker, cancellationToken);
			await test.TestCodeActionContext(ctx, invokeFixWithTitle, expectedFixCount, expectedTextAfterInvoke, typeText, expectedTextAfterTyping, cancellationToken);
		}

		// TODO: allow caller to provide a more limited set of analyzers to run
		public static async Task<CodeActionsWithContext> GetCodeActions (
			this MSBuildEditorTest test,
			IEnumerable<MSBuildAnalyzer> analyzers,
			IEnumerable<MSBuildCodeActionProvider> codeActionProviders,
			ITextView textView,
			SnapshotSpan range,
			IEnumerable<MSBuildCodeActionKind>? requestedKinds = null,
			bool includeCoreDiagnostics = false,
			MSBuildSchema? schema = null,
			ILogger? logger = null,
			CancellationToken cancellationToken = default)
		{
			logger ??= TestLoggerFactory.CreateTestMethodLogger ().RethrowExceptions ();

			var snapshot = textView.TextBuffer.CurrentSnapshot;
			var parsedDocument = MSBuildDocumentTest.ParseDocumentWithDiagnostics (snapshot.GetText (), analyzers, includeCoreDiagnostics, logger, schema, cancellationToken: cancellationToken);

			var sourceText = Microsoft.CodeAnalysis.Text.Extensions.AsText (range.Snapshot);
			var options = new VSEditorOptionsReader (textView.Options);

			var codeActionService = new MSBuildCodeActionService (codeActionProviders.ToArray ());
			var fixes = await codeActionService.GetCodeActions (sourceText, parsedDocument, new TextSpan(range.Start, range.End), requestedKinds, options, cancellationToken);

			return new CodeActionsWithContext (fixes, textView.TextBuffer, textView);
		}

		public static Task<CodeActionsWithContext> GetCodeActions<TAnalyzer,TCodeFix> (
			this MSBuildEditorTest test,
			ITextView textView,
			SnapshotSpan range,
			IEnumerable<MSBuildCodeActionKind>? requestedKinds = null,
			ILogger? logger = null,
			CancellationToken cancellationToken = default
			)
			where TAnalyzer : MSBuildAnalyzer, new()
			where TCodeFix : MSBuildCodeActionProvider, new()
		{
			return test.GetCodeActions ([new TAnalyzer ()], [new TCodeFix ()], textView, range, requestedKinds, false, null, logger, cancellationToken);
		}
		public static Task<CodeActionsWithContext> GetCodeActions<TAnalyzer, TCodeFix> (
			this MSBuildEditorTest test,
			string documentWithSelection,
			ISet<MSBuildCodeActionKind>? requestedKinds = null,
			char selectionMarker = '|',
			ILogger? logger = null,
			CancellationToken cancellationToken = default
			)
			where TAnalyzer : MSBuildAnalyzer, new()
			where TCodeFix : MSBuildCodeActionProvider, new()
		{
			var textView = test.CreateTextViewWithSelection (documentWithSelection, selectionMarker, allowZeroWidthSingleMarker: true);

			return test.GetCodeActions ([new TAnalyzer ()], [new TCodeFix ()], textView, textView.Selection.SelectedSpans.Single(), requestedKinds, false, null, logger, cancellationToken);
		}

		public static async Task TestCodeFix<TAnalyzer, TCodeAction> (
			this MSBuildEditorTest test,
			string documentWithSelection,
			string invokeFixWithTitle,
			int expectedFixCount,
			string expectedTextAfterInvoke,
			string? typeText = null,
			string? expectedTextAfterTyping = null,
			char selectionMarker = '|',
			CancellationToken cancellationToken = default
			)
			where TAnalyzer : MSBuildAnalyzer, new()
			where TCodeAction : MSBuildCodeActionProvider, new()
		{
			await test.Catalog.JoinableTaskContext.Factory.SwitchToMainThreadAsync ();
			var ctx = await test.GetCodeActions<TAnalyzer,TCodeAction> (documentWithSelection, selectionMarker: selectionMarker, cancellationToken: cancellationToken);
			await test.TestCodeActionContext (ctx, invokeFixWithTitle, expectedFixCount, expectedTextAfterInvoke, typeText, expectedTextAfterTyping, cancellationToken);
		}

		public static async Task TestCodeActionContext (
			this MSBuildEditorTest test,
			CodeActionsWithContext ctx,
			string invokeFixWithTitle,
			int expectedFixCount,
			string expectedTextAfterInvoke,
			string? typeText = null,
			string? expectedTextAfterTyping = null,
			CancellationToken cancellationToken = default
			)
		{
			if (!test.Catalog.JoinableTaskContext.IsOnMainThread) {
				throw new InvalidOperationException ("Must be on main thread");
			}

			Assert.That (ctx.CodeActions, Has.Count.EqualTo (expectedFixCount));
			Assert.That (ctx.CodeActions.Select (a => a.Title), Has.One.EqualTo (invokeFixWithTitle));

			var action = ctx.CodeActions.Single (a => a.Title == invokeFixWithTitle);

			var workspaceEdit = await action.ComputeOperationsAsync (cancellationToken);

			// TODO: check all edits are for the textView
			var edits = workspaceEdit.Operations.OfType<MSBuildDocumentEdit> ().SelectMany(d => d.TextEdits).ToList ();

			edits.Apply(ctx.TextBuffer, CancellationToken.None, ctx.TextView);

			Assert.That (
				ctx.TextBuffer.CurrentSnapshot.GetText (),
				Is.EqualTo (expectedTextAfterInvoke));

			if (typeText is null) {
				return;
			}

			if (expectedTextAfterTyping is null) {
				throw new ArgumentNullException (nameof(expectedTextAfterTyping), $"Argument '{expectedTextAfterTyping}' mus not be null when '{typeText}' is not null");
			}

			// the refactoring may have left multiple selections sp the user can e.g. type a new name for an extracted property
			await test.Catalog.JoinableTaskContext.Factory.SwitchToMainThreadAsync (default);
			var commandService = test.Catalog.CommandServiceFactory.GetService (ctx.TextView);
			commandService.Type (typeText);

			Assert.That (
				ctx.TextBuffer.CurrentSnapshot.GetText (),
				Is.EqualTo (expectedTextAfterTyping));
		}

		static void SetDefaultEditorOptions(ITextView textView)
		{
			var options = textView.Options;
			options.SetOptionValue (DefaultOptions.ConvertTabsToSpacesOptionId, true);
			options.SetOptionValue (DefaultOptions.IndentSizeOptionId, 2);
			options.SetOptionValue (DefaultOptions.TabSizeOptionId, 2);
		}
	}
}
