// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Language
{
	static class MSBuildNavigation
	{
		public static bool CanNavigate (MSBuildRootDocument doc, int offset, MSBuildResolveResult rr)
		{
			if (rr == null) {
				return false;
			}

			var annotations = GetAnnotationsAtOffset<NavigationAnnotation> (doc, offset);
			if (annotations != null && annotations.Any ()) {
				return true;
			}

			if (rr.ReferenceKind == MSBuildReferenceKind.Target) {
				return true;
			}

			if (rr.ReferenceKind == MSBuildReferenceKind.FileOrFolder) {
				return true;
			}

			if (rr.ReferenceKind == MSBuildReferenceKind.NuGetID) {
				return true;
			}

			if (rr.ReferenceKind == MSBuildReferenceKind.Task) {
				return true;
			}

			return false;
		}

		public static MSBuildNavigationResult GetNavigation (
			MSBuildRootDocument doc, int offset, MSBuildResolveResult rr)
		{
			if (rr == null) {
				return null;
			}

			var annotations = GetAnnotationsAtOffset<NavigationAnnotation> (doc, offset);
			var firstAnnotation = annotations.FirstOrDefault ();
			if (firstAnnotation != null) {
				var arr = GetAnnotatedPaths ().ToArray ();
				if (arr.Length == 0) {
					return null;
				}
				return new MSBuildNavigationResult (arr, firstAnnotation.Span.Start, firstAnnotation.Span.Length);
			}

			IEnumerable<string> GetAnnotatedPaths ()
			{
				foreach (var a in annotations) {
					if (a.IsSdk) {
						yield return Path.Combine (a.Path, "Sdk.props");
						yield return Path.Combine (a.Path, "Sdk.targets");
					} else {
						yield return a.Path;
					}
				}
			}

			if (rr.ReferenceKind == MSBuildReferenceKind.Target) {
				return new MSBuildNavigationResult (MSBuildReferenceKind.Target, (string)rr.Reference, rr.ReferenceOffset, rr.ReferenceLength);
			}

			if (rr.ReferenceKind == MSBuildReferenceKind.FileOrFolder) {
				return new MSBuildNavigationResult (
					(string[])rr.Reference, rr.ReferenceOffset, rr.ReferenceLength
				);
			}

			if (rr.ReferenceKind == MSBuildReferenceKind.Task) {
				var task = doc.GetTask ((string)rr.Reference);
				if (task.DeclaredInFile != null) {
					return new MSBuildNavigationResult (
						MSBuildReferenceKind.Task, (string)rr.Reference, rr.ReferenceOffset, rr.ReferenceLength,
						task.DeclaredInFile, task.DeclaredAtOffset
					);
				}
			}

			if (rr.ReferenceKind == MSBuildReferenceKind.NuGetID) {
				return new MSBuildNavigationResult (MSBuildReferenceKind.NuGetID, (string)rr.Reference, rr.ReferenceOffset, rr.ReferenceLength);
			}

			return null;
		}

		public static IEnumerable<T> GetAnnotationsAtOffset<T> (MSBuildRootDocument doc, int offset)
		{
			var xobj = doc.XDocument.FindNodeAtOffset (offset);
			if (xobj == null) {
				return null;
			}
			return doc.Annotations
				.GetMany<T> (xobj)
				.Where (a => !(a is IRegionAnnotation ra) || ra.Span.Contains (offset));
		}

		public static List<MSBuildNavigationResult> ResolveAll (MSBuildRootDocument doc, int offset, int length)
		{
			var visitor = new MSBuildNavigationVisitor ();
			visitor.Run (doc, offset, length);
			return visitor.Navigations;
		}

		class MSBuildNavigationVisitor : MSBuildResolvingVisitor
		{
			public List<MSBuildNavigationResult> Navigations { get; } = new List<MSBuildNavigationResult> ();

			protected override void VisitResolvedAttribute (
				XElement element, XAttribute attribute,
				MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
			{
				switch (resolvedElement.SyntaxKind) {
				case MSBuildSyntaxKind.Import:
					if (attribute.NameEquals ("Project", true)) {
						CaptureAnnotations ();
					}
					break;
				case MSBuildSyntaxKind.Project:
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
						foreach (var group in annotations.GroupBy (a => a.Span.Start)) {
							var first = group.First ();
							Navigations.Add (new MSBuildNavigationResult (
								group.Select (a => a.Path).ToArray (), first.Span.Start, first.Span.Length
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
						if (n is ExpressionText lit && lit.IsPure) {
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
					if (node is ListExpression list) {
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
				var path = MSBuildCompletionExtensions.EvaluateExpressionAsPaths (node, document).FirstOrDefault ();
				if (path != null && File.Exists (path)) {
					return new MSBuildNavigationResult (
						new[] { path }, node.Offset, node.Length
					);
				}
			} catch (Exception ex) {
				LoggingService.LogError ($"Error checking path for file '{node}'", ex);
			}
			return null;
		}
	}

	class MSBuildNavigationResult
	{
		public MSBuildNavigationResult (
			MSBuildReferenceKind kind, string name, int offset, int length,
			string destFile = null, int destOffset = default)
		{
			Kind = kind;
			Name = name;
			Offset = offset;
			Length = length;
			DestFile = destFile;
			DestOffset = destOffset;
		}

		public MSBuildNavigationResult (string[] paths, int offset, int length)
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
		public string[] Paths { get; }
		public string DestFile { get; }
		public int DestOffset { get; }
	}
}
