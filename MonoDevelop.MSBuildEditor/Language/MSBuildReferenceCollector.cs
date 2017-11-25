// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	abstract class MSBuildReferenceCollector : MSBuildResolvingVisitor
	{
		public List<(int Offset, int Length)> Results { get; } = new List<(int, int)> ();
		public string Name { get; }

		protected MSBuildReferenceCollector (string name)
		{
			if (string.IsNullOrEmpty (name)) {
				throw new ArgumentException ("Name cannot be null or empty", name);
			}
			Name = name;
		}

		protected bool IsMatch (string name) => string.Equals (name, Name, StringComparison.OrdinalIgnoreCase);
		protected bool IsMatch (INamedXObject obj) => IsMatch (obj.Name.Name);
		protected void AddResult (XElement el) => Results.Add ((el.GetNameStartOffset (Document), el.Name.Name.Length));
		protected void AddResult (XAttribute att) => Results.Add ((ConvertLocation (att.Region.Begin), att.Name.Name.Length));

		public static bool CanCreate (MSBuildResolveResult rr)
		{
			if (rr == null || rr.LanguageElement == null) {
				return false;
			}

			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Property:
			case MSBuildReferenceKind.Item:
			case MSBuildReferenceKind.Task:
			case MSBuildReferenceKind.Metadata:
				return !string.IsNullOrEmpty (rr.ReferenceName);
			}

			return false;
		}

		public static MSBuildReferenceCollector Create (MSBuildResolveResult rr)
		{
			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Property:
				return new MSBuildPropertyReferenceCollector (rr.ReferenceName);
			case MSBuildReferenceKind.Item:
				return new MSBuildItemReferenceCollector (rr.ReferenceName);
			case MSBuildReferenceKind.Metadata:
				return new MSBuildMetadataReferenceCollector (rr.ReferenceItemName, rr.ReferenceName);
			case MSBuildReferenceKind.Task:
				return new MSBuildTaskReferenceCollector (rr.ReferenceName);
			}

			throw new ArgumentException ($"Cannot create collector for resolve result", nameof (rr));
		}
	}

	class MSBuildItemReferenceCollector : MSBuildReferenceCollector
	{
		public MSBuildItemReferenceCollector (string itemName) : base (itemName) {}

		protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			if ((resolved.Kind == MSBuildKind.Item || resolved.Kind == MSBuildKind.ItemDefinition) && IsMatch (element.Name.Name)) {
				Results.Add ((element.GetNameStartOffset (Document), element.Name.Name.Length));
			}
			base.VisitResolvedElement (element, resolved);
		}

		protected override void VisitValueExpression (ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			foreach (var n in node.WithAllDescendants ()) {
				switch (n) {
				case ExpressionItem ei:
					if (IsMatch (ei.Name)) {
						Results.Add ((ei.NameOffset, ei.Name.Length));
					}
					break;
				case ExpressionMetadata em:
					if (em.IsQualified && IsMatch (em.ItemName)) {
						Results.Add ((em.ItemNameOffset, em.ItemName.Length));
					}
					break;
				}
			}
		}
	}

	class MSBuildPropertyReferenceCollector : MSBuildReferenceCollector
	{
		public MSBuildPropertyReferenceCollector (string propertyName) : base (propertyName) {}


		protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			if ((resolved.Kind == MSBuildKind.Property) && IsMatch (element.Name.Name)) {
				Results.Add ((element.GetNameStartOffset (Document), element.Name.Name.Length));
			}
			base.VisitResolvedElement (element, resolved);
		}

		protected override void VisitValueExpression (ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			foreach (var n in node.WithAllDescendants ()) {
				switch (n) {
				case ExpressionProperty ep:
					if (IsMatch (ep.Name)) {
						Results.Add ((ep.NameOffset, ep.Name.Length));
					}
					break;
				}
			}
		}
	}

	class MSBuildTaskReferenceCollector : MSBuildReferenceCollector
	{
		public MSBuildTaskReferenceCollector (string taskName) : base (taskName) {}

		protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			if ((resolved.Kind == MSBuildKind.Task || resolved.Kind == MSBuildKind.UsingTask) && IsMatch (element.Name.Name)) {
				Results.Add ((element.GetNameStartOffset (Document), element.Name.Name.Length));
			}
			base.VisitResolvedElement (element, resolved);
		}
	}

	class MSBuildMetadataReferenceCollector : MSBuildReferenceCollector
	{
		readonly string itemName;

		public MSBuildMetadataReferenceCollector (string itemName, string metadataName) : base (metadataName)
		{
			this.itemName = itemName;
		}

		protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			if (resolved.Kind == MSBuildKind.Metadata && IsMatch (element.Name.Name) && IsItemNameMatch (element.ParentElement ().Name.Name)) {
				AddResult (element);
			}
			base.VisitResolvedElement (element, resolved);
		}

		protected override void VisitResolvedAttribute (XElement element, XAttribute attribute, MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
		{
			if (resolvedAttribute.AbstractKind == MSBuildKind.Metadata && IsMatch (attribute.Name.Name) && IsItemNameMatch (element.Name.Name)) {
				AddResult (attribute);
			}
			base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);
		}

		protected override void VisitValueExpression (ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			foreach (var n in node.WithAllDescendants ()) {
				switch (n) {
				case ExpressionMetadata em:
					var iname = em.GetItemName ();
					if (iname != null && IsItemNameMatch (iname) && IsMatch (em.MetadataName)) {
						Results.Add ((em.MetadataNameOffset, em.MetadataName.Length));
					}
					break;
				}
			}
		}

		bool IsItemNameMatch (string name) => string.Equals (name, itemName, StringComparison.OrdinalIgnoreCase);
	}

	class MSBuildTargetDefinitionCollector : MSBuildReferenceCollector
	{
		public MSBuildTargetDefinitionCollector (string targetName) : base (targetName) {}

		protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			if (resolved.Kind == MSBuildKind.Target) {
				var nameAtt = element.Attributes.Get (new XName (Name), true);
				if (nameAtt != null && IsMatch (nameAtt.Value)) {
					Results.Add ((nameAtt.GetValueStartOffset (Document), Name.Length));
				}
			}
			base.VisitResolvedElement (element, resolved);
		}
	}
}