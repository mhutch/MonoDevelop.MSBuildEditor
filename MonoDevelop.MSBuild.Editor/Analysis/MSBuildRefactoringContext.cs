// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	readonly struct MSBuildRefactoringContext
	{
		readonly Action<MSBuildAction> reportRefactoring;
		public MSBuildDocument Document { get; }
		public XDocument XDocument { get; }
		public TextSpan SelectedSpan { get; }
		public CancellationToken CancellationToken { get; }

		// thse are pre-resolved so the refactrings don't all have to duplicate the work
		public XObject XObject { get; }
		public MSBuildElementSyntax ElementSyntax { get; }
		public MSBuildAttributeSyntax AttributeSyntax { get; }

		internal MSBuildRefactoringContext (
			MSBuildRootDocument document,
			TextSpan selectedSpan,
			Action<MSBuildAction> reportRefactoring,
			CancellationToken cancellationToken)
		{
			this.reportRefactoring = reportRefactoring;
			Document = document;
			XDocument = document.XDocument;
			SelectedSpan = selectedSpan;

			var xobj = document.XDocument.FindAtOrBeforeOffset (SelectedSpan.Start);
			if (xobj.Span.Contains (SelectedSpan.Start) || xobj is XElement el && el.OuterSpan.Contains (SelectedSpan.Start)) {
				XObject = xobj;
			} else {
				XObject = null;
			}

			if (XObject != null && MSBuildElementSyntax.Get (XObject) is ValueTuple<MSBuildElementSyntax, MSBuildAttributeSyntax> val) {
				(ElementSyntax, AttributeSyntax) = val;
			} else {
				ElementSyntax = null;
				AttributeSyntax = null;
			}

			CancellationToken = cancellationToken;
		}

		public void RegisterRefactoring (MSBuildAction action)
		{
			reportRefactoring (action);
		}
	}
}