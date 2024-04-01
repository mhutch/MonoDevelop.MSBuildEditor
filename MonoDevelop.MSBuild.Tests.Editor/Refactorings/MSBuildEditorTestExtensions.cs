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
using MonoDevelop.MSBuild.Editor.Analysis;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Util;
using MonoDevelop.Xml.Editor.Tests.Extensions;
using MonoDevelop.Xml.Tests;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Editor;

readonly record struct CodeFixesWithContext (List<MSBuildCodeFix> CodeFixes, ITextBuffer TextBuffer, ITextView TextView);

static class MSBuildEditorTestExtensions
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

	public static Task<CodeFixesWithContext> GetRefactorings<T> (this MSBuildEditorTest test, string documentWithSelection, char selectionMarker = '|', CancellationToken cancellationToken = default)
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
		) where T : MSBuildRefactoringProvider, new()
	{
		var ctx = await test.GetRefactorings<T> (documentWithSelection, selectionMarker, cancellationToken);
		await test.TestCodeFixContext(ctx, invokeFixWithTitle, expectedFixCount, expectedTextAfterInvoke, typeText, expectedTextAfterTyping, cancellationToken);
	}

	// TODO: allow caller to provide a more limited set of analyzers to run
	public static async Task<CodeFixesWithContext> GetCodeFixes (
		this MSBuildEditorTest test,
		ICollection<MSBuildAnalyzer> analyzers,
		ICollection<MSBuildFixProvider> codeFixes,
		ITextView textView,
		SnapshotSpan range,
		MSBuildDiagnosticSeverity
		requestedSeverities,
		bool includeCoreDiagnostics = false,
		MSBuildSchema? schema = null,
		ILogger? logger = null,
		CancellationToken cancellationToken = default)
	{
		logger ??= TestLoggerFactory.CreateTestMethodLogger ().RethrowExceptions ();

		var snapshot = textView.TextBuffer.CurrentSnapshot;
		var diagnostics = MSBuildDocumentTest.GetDiagnostics (snapshot.GetText (), out var parsedDocument, analyzers, includeCoreDiagnostics, logger, schema, null, cancellationToken);

		var codeFixService = new MSBuildCodeFixService (codeFixes.ToArray ());
		var fixes = await codeFixService.GetFixes (textView.TextBuffer, parsedDocument, diagnostics, range, requestedSeverities, cancellationToken);

		return new CodeFixesWithContext (fixes, textView.TextBuffer, textView);
	}

	public static Task<CodeFixesWithContext> GetCodeFixes<TAnalyzer,TCodeFix> (
		this MSBuildEditorTest test,
		ITextView textView,
		SnapshotSpan range,
		MSBuildDiagnosticSeverity requestedSeverities,
		ILogger? logger = null,
		CancellationToken cancellationToken = default
		)
		where TAnalyzer : MSBuildAnalyzer, new()
		where TCodeFix : MSBuildFixProvider, new()
	{
		return test.GetCodeFixes ([new TAnalyzer ()], [new TCodeFix ()], textView, range, requestedSeverities, false, null, logger, cancellationToken);
	}
	public static Task<CodeFixesWithContext> GetCodeFixes<TAnalyzer, TCodeFix> (
		this MSBuildEditorTest test,
		string documentWithSelection,
		MSBuildDiagnosticSeverity requestedSeverities = MSBuildDiagnosticSeverity.All,
		char selectionMarker = '|',
		ILogger? logger = null,
		CancellationToken cancellationToken = default
		)
		where TAnalyzer : MSBuildAnalyzer, new()
		where TCodeFix : MSBuildFixProvider, new()
	{
		var textView = test.CreateTextViewWithSelection (documentWithSelection, selectionMarker);

		return test.GetCodeFixes ([new TAnalyzer ()], [new TCodeFix ()], textView, textView.Selection.SelectedSpans.Single(), requestedSeverities, false, null, logger, cancellationToken);
	}

	public static async Task TestCodeFix<TAnalyzer, TCodeFix> (
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
		where TCodeFix : MSBuildFixProvider, new()
	{
		var ctx = await test.GetCodeFixes<TAnalyzer,TCodeFix> (documentWithSelection, selectionMarker: selectionMarker, cancellationToken: cancellationToken);
		await test.TestCodeFixContext (ctx, invokeFixWithTitle, expectedFixCount, expectedTextAfterInvoke, typeText, expectedTextAfterTyping, cancellationToken);
	}

	public static async Task TestCodeFixContext (
		this MSBuildEditorTest test,
		CodeFixesWithContext ctx,
		string invokeFixWithTitle,
		int expectedFixCount,
		string expectedTextAfterInvoke,
		string? typeText = null,
		string? expectedTextAfterTyping = null,
		CancellationToken cancellationToken = default
		)
	{
		Assert.That (ctx.CodeFixes, Has.Count.EqualTo (expectedFixCount));
		Assert.That (ctx.CodeFixes.Select (c => c.Action.Title), Has.One.EqualTo (invokeFixWithTitle));

		var fix = ctx.CodeFixes.Single (c => c.Action.Title == invokeFixWithTitle);

		var operations = await fix.Action.ComputeOperationsAsync (cancellationToken);

		var options = ctx.TextView.Options;
		options.SetOptionValue (DefaultOptions.ConvertTabsToSpacesOptionId, true);
		options.SetOptionValue (DefaultOptions.IndentSizeOptionId, 2);
		options.SetOptionValue (DefaultOptions.TabSizeOptionId, 2);

		foreach (var op in operations) {
			op.Apply (options, ctx.TextBuffer, CancellationToken.None, ctx.TextView);
		}

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
}
