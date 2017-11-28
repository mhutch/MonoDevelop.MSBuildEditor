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
					MSBuildReferenceKind.Target, rr.ReferenceName, rr.ReferenceOffset, rr.ReferenceName.Length
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
				foreach (var n in node.WithAllDescendants ()) {
					if (n is ExpressionLiteral lit && lit.IsPure) {
						var r = GetNavigationFromValue (kind, lit.Value, lit.Offset, Filename);
						if (r != null) {
							Navigations.Add (r);
						}
					}
				}
			}
		}

		static MSBuildNavigationResult GetNavigationFromValue (MSBuildValueKind kind, string value, int offset, string filename)
		{
			switch (kind.GetScalarType ()) {
			case MSBuildValueKind.TargetName:
				return new MSBuildNavigationResult (
					MSBuildReferenceKind.Target, value, offset, value.Length
				);
			case MSBuildValueKind.File:
			case MSBuildValueKind.FileOrFolder:
			case MSBuildValueKind.ProjectFile:
			case MSBuildValueKind.TaskAssemblyFile:
				try {
					var path = Projects.MSBuild.MSBuildProjectService.FromMSBuildPath (
						Path.GetDirectoryName (filename), value
					);
					if (File.Exists (path)) {
						return new MSBuildNavigationResult (
							new [] { path }, offset, value.Length
						);
					}
				} catch (Exception ex) {
					Core.LoggingService.LogError ($"Error checking path for file '{value}'", ex);
				}
				break;
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
