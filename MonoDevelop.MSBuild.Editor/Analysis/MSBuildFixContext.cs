// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

using Microsoft.VisualStudio.Text;
using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	struct MSBuildFixContext
	{
		readonly Action<MSBuildAction, ImmutableArray<MSBuildDiagnostic>> reportFix;
		public MSBuildDocument Document { get; }
		public XDocument XDocument { get; }
		public TextSpan Span { get; }
		public ImmutableArray<MSBuildDiagnostic> Diagnostics { get; }
		public CancellationToken CancellationToken { get; }

		/// <summary>
		/// DO NOT USE. This is workaround for the spellchecker. It can be removed when
		/// there is a better concept of a durable context/scope to which information can be bound.
		/// </summary>
		internal ITextBuffer Buffer { get; }

		internal MSBuildFixContext (
			ITextBuffer buffer,
			MSBuildDocument document,
			XDocument xDocument,
			TextSpan span,
			ImmutableArray<MSBuildDiagnostic> diagnostics,
			Action<MSBuildAction, ImmutableArray<MSBuildDiagnostic>> reportFix,
			CancellationToken cancellationToken)
		{
			this.Buffer = buffer;
			this.reportFix = reportFix;
			Document = document;
			XDocument = xDocument;
			Span = span;
			Diagnostics = diagnostics;
			CancellationToken = cancellationToken;
		}

		public void RegisterCodeFix (MSBuildAction action, ImmutableArray<MSBuildDiagnostic> diagnostics)
			=> reportFix (action, diagnostics);

		public void RegisterCodeFix (MSBuildAction action, MSBuildDiagnostic diagnostic)
			=> reportFix (action, ImmutableArray.Create (diagnostic));

		public void RegisterCodeFix (MSBuildAction action, IEnumerable<MSBuildDiagnostic> diagnostics)
			=> reportFix (action, ImmutableArray.CreateRange (diagnostics));
	}
}