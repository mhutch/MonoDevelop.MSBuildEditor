// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	readonly struct MSBuildRefactoringContext
	{
		readonly Action<MSBuildAction> reportRefactoring;
		public MSBuildDocument Document { get; }
		public TextSpan Span { get; }
		public CancellationToken CancellationToken { get; }

		readonly List<XNode> nodes;
		readonly Dictionary<XElement, MSBuildElementSyntax> elements;

		public IReadOnlyList<XNode> NodesInSpan => nodes;
		public IReadOnlyDictionary<XElement,MSBuildElementSyntax> ElementsinSpan => elements;

		internal MSBuildRefactoringContext (
			MSBuildRootDocument document,
			TextSpan span,
			Action<MSBuildAction> reportRefactoring,
			CancellationToken cancellationToken)
		{
			this.reportRefactoring = reportRefactoring;
			Document = document;
			Span = span;

			nodes = new List<XNode> ();
			nodes.AddRange (GetNodesIntersectingRange (document.XDocument, span));
			elements = new Dictionary<XElement, MSBuildElementSyntax> ();
			foreach (var n in nodes) {
				if (n is XElement el) {
					elements.Add (el, GetSyntax (el));
				}
			}

			CancellationToken = cancellationToken;
		}

		static MSBuildElementSyntax GetSyntax (XElement el)
		{
			if (el.Parent is XDocument && el.NameEquals (MSBuildElementSyntax.Project.Name, true)) {
				return MSBuildElementSyntax.Project;
			}
			var parentSyntax = el.Parent is XElement p ? GetSyntax (p) : null;
			if (parentSyntax != null) {
				return MSBuildElementSyntax.Get (el.Name.Name, parentSyntax);
			}
			return null;
		}

		static IEnumerable<XNode> GetNodesIntersectingRange (XDocument xDocument, TextSpan span)
		{
			var startObj = xDocument.FindAtOrBeforeOffset (span.Start);
			var node = startObj as XNode ?? startObj.Parents.OfType<XNode> ().First ();

			foreach (var n in LinearWalkFrom (node)) {
				if (n.Span.Start > span.End) {
					yield break;
				}
				if (n.Span.Intersects (span)) {
					yield return n;
				}
			}
		}

		static IEnumerable<XNode> LinearWalkFrom (XNode node)
		{
			while (node != null) {
				yield return node;
				if (node is XContainer c) {
					foreach (var n in c.AllDescendentNodes) {
						yield return n;
					}
				}
				yield return node;
				while (node != null && node.NextSibling == null) {
					node = (XNode)node.Parent as XNode;
				}
				node = node?.NextSibling;
			}
		}

		public void RegisterRefactoring (MSBuildAction action)
		{
			reportRefactoring (action);
		}
	}
}