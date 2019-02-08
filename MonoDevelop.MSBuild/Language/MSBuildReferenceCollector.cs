// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Language
{
	enum ReferenceUsage
	{
		Declaration,
		Read,
		Write
	}

	abstract class MSBuildReferenceCollector : MSBuildResolvingVisitor
	{
		public List<(int Offset, int Length, ReferenceUsage Usage)> Results { get; } = new List<(int, int, ReferenceUsage)> ();
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
		protected void AddResult (XElement el, ReferenceUsage usage) => Results.Add ((el.Span.Start + 1, el.Name.Name.Length, usage));
		protected void AddResult (XAttribute att, ReferenceUsage usage) => Results.Add ((att.Span.Start, att.Name.Name.Length, usage));

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
			case MSBuildReferenceKind.Target:
			case MSBuildReferenceKind.ItemFunction:
			case MSBuildReferenceKind.PropertyFunction:
			case MSBuildReferenceKind.StaticPropertyFunction:
			case MSBuildReferenceKind.ClassName:
			case MSBuildReferenceKind.Enum:
				return rr.Reference != null;
			}

			return false;
		}

		public static MSBuildReferenceCollector Create (MSBuildResolveResult rr)
		{
			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Property:
				return new MSBuildPropertyReferenceCollector ((string)rr.Reference);
			case MSBuildReferenceKind.Item:
				return new MSBuildItemReferenceCollector ((string)rr.Reference);
			case MSBuildReferenceKind.Metadata:
				var m = rr.ReferenceAsMetadata;
				return new MSBuildMetadataReferenceCollector (m.itemName, m.metaName);
			case MSBuildReferenceKind.Task:
				return new MSBuildTaskReferenceCollector ((string)rr.Reference);
			case MSBuildReferenceKind.Target:
				return new MSBuildTargetReferenceCollector ((string)rr.Reference);
			case MSBuildReferenceKind.ItemFunction:
				return new MSBuildItemFunctionReferenceCollector ((string)rr.Reference);
			case MSBuildReferenceKind.StaticPropertyFunction:
				(string className, string name) = ((string, string))rr.Reference;
				return new MSBuildStaticPropertyFunctionReferenceCollector (className, name);
			case MSBuildReferenceKind.PropertyFunction:
				(MSBuildValueKind valueKind, string funcName) = ((MSBuildValueKind, string))rr.Reference;
				return new MSBuildPropertyFunctionReferenceCollector (valueKind, funcName);
			case MSBuildReferenceKind.ClassName:
				return new MSBuildClassReferenceCollector ((string)rr.Reference);
			case MSBuildReferenceKind.Enum:
				return new MSBuildEnumReferenceCollector ((string)rr.Reference);
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
				Results.Add ((element.GetNameOffset(), element.Name.Name.Length, ReferenceUsage.Write));
			}
			base.VisitResolvedElement (element, resolved);
		}

		protected override void VisitValueExpression (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute,
			ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			foreach (var n in node.WithAllDescendants ()) {
				switch (n) {
				case ExpressionItemName ei:
					if (IsMatch (ei.Name)) {
						Results.Add ((ei.Offset, ei.Name.Length, ReferenceUsage.Read));
					}
					break;
				case ExpressionMetadata em:
					if (em.IsQualified && IsMatch (em.ItemName)) {
						Results.Add ((em.ItemNameOffset, em.ItemName.Length, ReferenceUsage.Read));
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
				Results.Add ((element.GetNameOffset (), element.Name.Name.Length, ReferenceUsage.Write));
			}
			base.VisitResolvedElement (element, resolved);
		}

		protected override void VisitValueExpression (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute,
			ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			foreach (var n in node.WithAllDescendants ()) {
				switch (n) {
				case ExpressionPropertyName ep:
					if (IsMatch (ep.Name)) {
						Results.Add ((ep.Offset, ep.Length, ReferenceUsage.Read));
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
			switch (resolved.Kind) {
			case MSBuildKind.Task:
				if (IsMatch (element.Name.Name)) {
					Results.Add ((element.GetNameOffset (), element.Name.Name.Length, ReferenceUsage.Read));
				}
				break;
			case MSBuildKind.UsingTask:
				var nameAtt = element.Attributes.Get (new XName ("TaskName"), true);
				if (nameAtt != null && !string.IsNullOrEmpty (nameAtt.Value)) {
					var nameIdx = nameAtt.Value.LastIndexOf ('.') + 1;
					string name = nameIdx > 0 ? nameAtt.Value.Substring (nameIdx) : nameAtt.Value;
					if (IsMatch (name)) {
						Results.Add ((nameAtt.GetValueOffset () + nameIdx, name.Length, ReferenceUsage.Declaration));
					}
				}
				break;
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
				AddResult (element, ReferenceUsage.Write);
			}
			base.VisitResolvedElement (element, resolved);
		}

		protected override void VisitResolvedAttribute (XElement element, XAttribute attribute, MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
		{
			if (resolvedAttribute.AbstractKind == MSBuildKind.Metadata && IsMatch (attribute.Name.Name) && IsItemNameMatch (element.Name.Name)) {
				AddResult (attribute, ReferenceUsage.Write);
			}
			base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);
		}

		protected override void VisitValueExpression (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute,
			ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			foreach (var n in node.WithAllDescendants ()) {
				switch (n) {
				case ExpressionMetadata em:
					var iname = em.GetItemName ();
					if (iname != null && IsItemNameMatch (iname) && IsMatch (em.MetadataName)) {
						Results.Add ((em.MetadataNameOffset, em.MetadataName.Length, ReferenceUsage.Read));
					}
					break;
				}
			}
		}

		bool IsItemNameMatch (string name) => string.Equals (name, itemName, StringComparison.OrdinalIgnoreCase);
	}

	class MSBuildTargetReferenceCollector : MSBuildReferenceCollector
	{
		public MSBuildTargetReferenceCollector (string targetName) : base (targetName) { }

		protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			if (resolved.Kind == MSBuildKind.Target) {
				var nameAtt = element.Attributes.Get (new XName (Name), true);
				if (nameAtt != null && IsMatch (nameAtt.Value)) {
					Results.Add ((nameAtt.GetValueOffset (), Name.Length, ReferenceUsage.Declaration));
				}
			}
			base.VisitResolvedElement (element, resolved);
		}

		protected override void VisitValueExpression (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute,
			ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			if (kind.GetScalarType () != MSBuildValueKind.TargetName) {
				return;
			}

			switch (node) {
			case ExpressionList list:
				foreach (var c in list.Nodes) {
					if (c is ExpressionText l) {
						CheckMatch (l);
						break;
					}
				}
				break;
			case ExpressionText lit:
				CheckMatch (lit);
				break;
			}
		}

		void CheckMatch (ExpressionText node)
		{
			//FIXME: get rid of this trim
			if (IsMatch (node.Value.Trim ())) {
				Results.Add ((node.Offset, node.Length, ReferenceUsage.Read));
			}
		}
	}

	class MSBuildTargetDefinitionCollector : MSBuildReferenceCollector
	{
		public MSBuildTargetDefinitionCollector (string targetName) : base (targetName) {}

		protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			if (resolved.Kind == MSBuildKind.Target) {
				var nameAtt = element.Attributes.Get (new XName ("Name"), true);
				if (nameAtt != null && IsMatch (nameAtt.Value)) {
					Results.Add ((nameAtt.GetValueOffset (), Name.Length, ReferenceUsage.Declaration));
				}
			}
			base.VisitResolvedElement (element, resolved);
		}
	}

	class MSBuildStaticPropertyFunctionReferenceCollector : MSBuildReferenceCollector
	{
		readonly string className;

		public MSBuildStaticPropertyFunctionReferenceCollector (string className, string functionName)
			: base (MSBuildPropertyFunctionReferenceCollector.StripGetPrefix (functionName))
		{
			this.className = className;
		}

		protected override void VisitValueExpression (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute,
			ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			foreach (var n in node.WithAllDescendants ()) {
				switch (n) {
				case ExpressionFunctionName func:
					if (func.Parent is ExpressionPropertyFunctionInvocation inv) {
						string baseName = MSBuildPropertyFunctionReferenceCollector.StripGetPrefix (func.Name);
						if (IsMatch (baseName) && inv.Target is ExpressionClassReference cn && cn.Name == className) {
							Results.Add ((func.Offset, func.Length, ReferenceUsage.Read));
						}
					}
					break;
				}
			}
		}
	}

	class MSBuildPropertyFunctionReferenceCollector : MSBuildReferenceCollector
	{
		//ensure that props and function-style props are equivalent e.g get_Now() and Now
		//this is kinda hacky, we should really check whether the get variant is a
		//method and the non-get variant is a property
		internal static string StripGetPrefix (string name)
		{
			if (name.StartsWith ("get_", StringComparison.Ordinal) && name.Length > 4) {
				return name.Substring (4);
			}
			return name;
		}

		readonly MSBuildValueKind valueKind;

		public MSBuildPropertyFunctionReferenceCollector (MSBuildValueKind valueKind, string functionName)
			: base (StripGetPrefix (functionName))
		{
			if (valueKind == MSBuildValueKind.Unknown) {
				valueKind = MSBuildValueKind.String;
			}
			this.valueKind = valueKind;
		}

		protected override void VisitValueExpression (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute,
			ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			foreach (var n in node.WithAllDescendants ()) {
				switch (n) {
				case ExpressionFunctionName func:
					if (func.Parent is ExpressionPropertyFunctionInvocation inv) {
						string baseName = StripGetPrefix (func.Name);
						if (IsMatch (baseName)) {
							//TODO: should we be fuzzy here and accept "unknown"?
							var resolvedKind = FunctionCompletion.ResolveType (inv);
							if (resolvedKind == MSBuildValueKind.Unknown) {
								resolvedKind = MSBuildValueKind.String;
							}
							if (resolvedKind == valueKind) {
								Results.Add ((func.Offset, func.Length, ReferenceUsage.Read));
							}
						}
					}
					break;
				}
			}
		}
	}

	class MSBuildItemFunctionReferenceCollector : MSBuildReferenceCollector
	{
		public MSBuildItemFunctionReferenceCollector (string functionName) : base (functionName) { }

		protected override void VisitValueExpression (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute,
			ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			foreach (var n in node.WithAllDescendants ()) {
				switch (n) {
				case ExpressionFunctionName func:
					if (func.Parent is ExpressionItemFunctionInvocation inv && IsMatch (func.Name)) {
						Results.Add ((func.Offset, func.Length, ReferenceUsage.Read));
					}
					break;
				}
			}
		}
	}

	class MSBuildClassReferenceCollector : MSBuildReferenceCollector
	{
		public MSBuildClassReferenceCollector (string className) : base (className) { }

		protected override void VisitValueExpression (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute,
			ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			foreach (var n in node.WithAllDescendants ()) {
				switch (n) {
				case ExpressionClassReference classRef:
					if (IsMatch (classRef.Name) && classRef.Parent is ExpressionPropertyFunctionInvocation) {
						Results.Add ((classRef.Offset, classRef.Length, ReferenceUsage.Read));
					}
					break;
				}
			}
		}
	}

	class MSBuildEnumReferenceCollector : MSBuildReferenceCollector
	{
		public MSBuildEnumReferenceCollector (string enumName) : base (enumName) { }

		protected override void VisitValueExpression (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute,
			ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			foreach (var n in node.WithAllDescendants ()) {
				switch (n) {
				case ExpressionClassReference classRef:
					if (IsMatch (classRef.Name) && classRef.Parent is ExpressionArgumentList) {
						Results.Add ((classRef.Offset, classRef.Length, ReferenceUsage.Read));
					}
					break;
				}
			}
		}
	}
}