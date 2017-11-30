// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;
using System.IO;
using System;

namespace MonoDevelop.MSBuildEditor.Language
{
	static class MSBuildNavigation
	{
		public static bool CanNavigate (MSBuildRootDocument doc, DocumentLocation location, MSBuildResolveResult rr)
		{
			if (rr == null) {
				return false;
			}

			var annotations = GetAnnotationsAtLocation<NavigationAnnotation> (doc, location);
			if (annotations != null && annotations.Any ()) {
				return true;
			}

			if (rr.ReferenceKind == MSBuildReferenceKind.Target) {
				return true;
			}

			return false;
		}

		public static MSBuildNavigationResult GetNavigation (
			MSBuildRootDocument doc, DocumentLocation location, MSBuildResolveResult rr)
		{
			if (rr == null) {
				return null;
			}

			//HACK: we should really use the ITextSource directly, but since the XML parser positions are
			//currently line/col, we need a TextDocument to convert to offsets
			var textDocument = doc.Text as IReadonlyTextDocument
				?? TextEditorFactory.CreateNewReadonlyDocument (
					doc.Text, doc.Filename, MSBuildTextEditorExtension.MSBuildMimeType
				);

			var annotations = GetAnnotationsAtLocation<NavigationAnnotation> (doc, location);
			var firstAnnotation = annotations.FirstOrDefault ();
			if (firstAnnotation != null) {
				var beginOffset = textDocument.LocationToOffset (firstAnnotation.Region.Begin);
				var endOffset = textDocument.LocationToOffset (firstAnnotation.Region.End);
				return new MSBuildNavigationResult (
					annotations.Select (a => a.Path).ToArray (), beginOffset, endOffset - beginOffset + 1
				);
			}
			if (rr.ReferenceKind == MSBuildReferenceKind.Target) {
				return new MSBuildNavigationResult (
					MSBuildReferenceKind.Target, (string)rr.Reference, rr.ReferenceOffset, rr.ReferenceLength
				);
			}
			return null;
		}

		public static IEnumerable<T> GetAnnotationsAtLocation<T> (MSBuildRootDocument doc, DocumentLocation location)
		{
			var xobj = doc.XDocument.FindNodeAtLocation (location);
			if (xobj == null) {
				return null;
			}
			return doc.Annotations
				.GetMany<T> (xobj)
				.Where (a => !(a is IRegionAnnotation ra) || ra.Region.Contains (location));
		}

		public static List<MSBuildNavigationResult> ResolveAll (MSBuildRootDocument doc)
		{
			var visitor = new MSBuildNavigationVisitor ();
			visitor.Run (doc);
			return visitor.Navigations;
		}

		class MSBuildNavigationVisitor : MSBuildResolvingVisitor
		{
			public List<MSBuildNavigationResult> Navigations { get; } = new List<MSBuildNavigationResult> ();

			protected override void VisitResolvedAttribute (
				XElement element, XAttribute attribute,
				MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
			{
				switch (resolvedElement.Kind) {
				case MSBuildKind.Import:
					if (attribute.NameEquals ("Project", true)) {
						CaptureAnnotations ();
					}
					break;
				case MSBuildKind.Project:
					if (attribute.NameEquals ("Sdk", true)) {
						CaptureAnnotations ();
					}
					break;
				}

				base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);

				void CaptureAnnotations ()
				{
					var annotations = Document.Annotations.GetMany<NavigationAnnotation> (attribute);
					if (annotations != null) {
						foreach (var group in annotations.GroupBy (a => a.Region.Begin)) {
							var first = group.First ();
							var beginOffset = ConvertLocation (first.Region.Begin);
							var endOffset = ConvertLocation (first.Region.End);
							Navigations.Add (new MSBuildNavigationResult (
								group.Select (a => a.Path).ToArray (), beginOffset, endOffset - beginOffset + 1
							));
						}
					}
				}
			}

			protected override void VisitValueExpression (XElement element, XAttribute attribute, MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute, ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
			{
				switch (kind.GetScalarType ()) {
				case MSBuildValueKind.TargetName:
					foreach (var n in node.WithAllDescendants ()) {
						if (n is ExpressionLiteral lit && lit.IsPure) {
							Navigations.Add (new MSBuildNavigationResult (
								MSBuildReferenceKind.Target, lit.Value, lit.Offset, lit.Length
							));
						}
					}
					break;
				case MSBuildValueKind.File:
				case MSBuildValueKind.FileOrFolder:
				case MSBuildValueKind.ProjectFile:
				case MSBuildValueKind.TaskAssemblyFile:
				case MSBuildValueKind.Unknown:
					if (node is ExpressionList list) {
						foreach (var n in list.Nodes) {
							var p = GetPathFromNode (n, (MSBuildRootDocument)Document);
							if (p != null) {
								Navigations.Add (p);
							}
						}
					}
					var path = GetPathFromNode (node, (MSBuildRootDocument)Document);
					if (path != null) {
						Navigations.Add (path);
					}
					break;
				}
			}
		}

		public static MSBuildNavigationResult GetPathFromNode (ExpressionNode node, MSBuildRootDocument document)
		{
			try {
				var path = MSBuildCompletionExtensions.EvaluateExpressionAsPath (node, document);
				if (path != null && File.Exists (path)) {
					return new MSBuildNavigationResult (
						new [] { path }, node.Offset, node.Length
					);
				}
			} catch (Exception ex) {
				Core.LoggingService.LogError ($"Error checking path for file '{node}'", ex);
			}
			return null;
		}
	}

	class MSBuildNavigationResult
	{
		public MSBuildNavigationResult (MSBuildReferenceKind kind, string name, int offset, int length)
		{
			Kind = kind;
			Name = name;
			Offset = offset;
			Length = length;
		}

		public MSBuildNavigationResult (string [] paths, int offset, int length)
		{
			Kind = MSBuildReferenceKind.None;
			Paths = paths;
			Offset = offset;
			Length = length;
		}

		public MSBuildReferenceKind Kind { get; }
		public string Name { get; }
		public int Offset { get; }
		public int Length { get; }
		public string [] Paths { get; }
	}
}
