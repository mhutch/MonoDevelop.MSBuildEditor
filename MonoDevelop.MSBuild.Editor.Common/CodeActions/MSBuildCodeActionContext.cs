// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Options;

using TextSpan = MonoDevelop.Xml.Dom.TextSpan;

namespace MonoDevelop.MSBuild.Editor.CodeActions
{
	class MSBuildCodeActionContext
	{
		readonly Action<MSBuildCodeAction> reportAction;

		public SourceText SourceText { get; }
		public MSBuildDocument Document { get; }
		public XDocument XDocument { get; }

		/// <summary>
		/// A span of text. This may be the caret location, a selection, or an entire line.
		/// </summary>
		public TextSpan Span { get; }

		/// <summary>
		/// Diagnostics that intersect with the span
		/// </summary>
		public IReadOnlyList<MSBuildDiagnostic> AllDiagnosticsInSpan { get; }

		public IEnumerable<MSBuildDiagnostic> GetMatchingDiagnosticsInSpan(IEnumerable<string> diagnosticIds)
			=> AllDiagnosticsInSpan.Where(d => diagnosticIds.Any(id => d.Descriptor.Id == id));

		/// <summary>
		/// If a <see cref="MSBuildCodeActionProvider"/> registers multiple values for
		/// <see cref="MSBuildCodeActionProvider.ProducedCodeActionKinds"/>, it should check this
		/// before generating actions.
		/// </summary>
		public ISet<MSBuildCodeActionKind> RequestedActionKinds { get; }

		public IOptionsReader Options { get; }
		public TextFormattingOptionValues TextFormat { get; }
		public CancellationToken CancellationToken { get; }

		// these are pre-resolved so various refactorings don't all have to duplicate the work

		/// <summary>
		/// The <see cref="SpanStartXObject"/> whose span includes the start of the <see cref="Span"/>.
		/// </summary>
		public XObject? SpanStartXObject { get; }

		/// <summary>
		/// If <see cref="SpanStartXObject"/> is non-null, and is MSBuild element or attribute syntax, this identifies its element syntax type.
		/// </summary>
		public MSBuildElementSyntax? SpanStartElementSyntax { get; }

		/// <summary>
		/// If <see cref="SpanStartXObject"/> is non-null, and is MSBuild attribute syntax, this identifies its attribute syntax type.
		/// </summary>
		public MSBuildAttributeSyntax? SpanStartAttributeSyntax { get; }

		internal MSBuildCodeActionContext (
			SourceText sourceText,
			MSBuildRootDocument document,
			TextSpan span,
			ISet<MSBuildCodeActionKind> requestedKinds,
			IOptionsReader options,
			Action<MSBuildCodeAction> reportAction,
			CancellationToken cancellationToken)
		{
			SourceText = sourceText;
			Document = document;
			XDocument = document.XDocument;
			Span = span;

			RequestedActionKinds = requestedKinds;

			var allDiagnostics = document.Diagnostics ?? throw new ArgumentException ("document must have diagnostics");
			AllDiagnosticsInSpan = allDiagnostics.Where (d => span.Intersects (d.Span)).ToArray ();

			Options = options;
			TextFormat = new TextFormattingOptionValues (options);

			var xobj = document.XDocument.FindAtOrBeforeOffset (Span.Start);
			if (xobj.Span.Contains (Span.Start) || xobj is XElement el && el.OuterSpan.Contains (Span.Start)) {
				SpanStartXObject = xobj;
			} else {
				SpanStartXObject = null;
			}

			if (SpanStartXObject != null && MSBuildElementSyntax.Get (SpanStartXObject) is ValueTuple<MSBuildElementSyntax, MSBuildAttributeSyntax> val) {
				(SpanStartElementSyntax, SpanStartAttributeSyntax) = val;
			} else {
				SpanStartElementSyntax = null;
				SpanStartAttributeSyntax = null;
			}

			this.reportAction = reportAction;

			CancellationToken = cancellationToken;
		}

		public void RegisterCodeAction (MSBuildCodeAction action) => reportAction (action);
	}
}