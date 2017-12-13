// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuildEditor.Language
{
	static class MSBuildResolver
	{
		static System.Reflection.PropertyInfo ParentProp = typeof (XObject).GetProperty ("Parent");

		public static MSBuildResolveResult Resolve (
			XmlParser parser, IReadonlyTextDocument document, MSBuildDocument context)
		{
			int offset = parser.Position;

			//clones and connects nodes to their parents
			parser = parser.GetTreeParser ();

			var nodePath = parser.Nodes.ToList ();
			nodePath.Reverse ();

			//capture incomplete names, attributes and element values
			int i = offset;
			if (parser.CurrentState is XmlRootState && parser.Nodes.Peek () is XElement unclosedEl) {
				while (i < document.Length && InRootOrClosingTagState () && !unclosedEl.IsClosed) {
					parser.Push (document.GetCharAt (i++));
				}
			} else {
				while (i < document.Length && InNameOrAttributeState ()) {
					parser.Push (document.GetCharAt (i++));
				}
			}

			//if nodes are incomplete, they won't get connected
			//HACK: the only way to reconnect them is reflection
			if (nodePath.Count > 1) {
				for (int idx = 1; idx < nodePath.Count; idx++) {
					var node = nodePath [idx];
					if (node.Parent == null) {
						var parent = nodePath [idx - 1];
						ParentProp.SetValue (node, parent);
					}
				}
			}

			//need to look up element by walking how the path, since at each level, if the parent has special children,
			//then that gives us information to identify the type of its children
			MSBuildLanguageElement languageElement = null;
			MSBuildLanguageAttribute languageAttribute = null;
			XElement el = null;
			XAttribute att = null;

			foreach (var node in nodePath) {
				if (node is XAttribute xatt && xatt.Name.Prefix == null) {
					att = xatt;
					languageAttribute = languageElement?.GetAttribute (att.Name.Name);
					break;
				}

				//if children of parent is known to be arbitrary data, don't go into it
				if (languageElement != null && languageElement.ValueKind == MSBuildValueKind.Data) {
					break;
				}
				
				//code completion is forgiving, all we care about best guess resolve for deepest child
				if (node is XElement xel && xel.Name.Prefix == null) {
					el = xel;
					languageElement = MSBuildLanguageElement.Get (el.Name.Name, languageElement);
					if (languageElement != null)
						continue;
				}

				languageElement = null;
			}

			if (languageElement == null) {
				return null;
			}

			var rr = new MSBuildResolveResult {
				LanguageElement = languageElement,
				LanguageAttribute = languageAttribute,
				XElement = el,
				XAttribute = att
			};

			var rv = new MSBuildResolveVisitor (offset, rr);
			rv.Run (el, languageElement, document.FileName, document, context);

			return rr;

			bool InNameOrAttributeState () =>
				parser.CurrentState is XmlNameState
				|| parser.CurrentState is XmlAttributeState
				|| parser.CurrentState is XmlAttributeValueState;

			bool InRootOrClosingTagState () =>
				parser.CurrentState is XmlRootState
				|| parser.CurrentState is XmlNameState
				|| parser.CurrentState is XmlClosingTagState;
		}

		class MSBuildResolveVisitor : MSBuildResolvingVisitor
		{
			int offset;
			readonly MSBuildResolveResult rr;

			public MSBuildResolveVisitor (int offset, MSBuildResolveResult rr)
			{
				this.offset = offset;
				this.rr = rr;
			}

			bool IsIn (int start, int length) => offset >= start && offset <= (start + length);

			protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
			{
				var start = ConvertLocation (element.Region.Begin) + 1;
				bool inName = IsIn (start, element.Name.Name.Length);
				if (inName) {
					rr.ReferenceOffset = start;
					rr.Reference = element.Name.Name;
					rr.ReferenceLength = element.Name.Name.Length;
					switch (resolved.Kind) {
					case MSBuildKind.Item:
					case MSBuildKind.ItemDefinition:
						rr.ReferenceKind = MSBuildReferenceKind.Item;
						return;
					case MSBuildKind.Metadata:
						rr.ReferenceKind = MSBuildReferenceKind.Metadata;
						rr.Reference = Tuple.Create (element.ParentElement ().Name.Name, element.Name.Name);
						return;
					case MSBuildKind.Task:
						rr.ReferenceKind = MSBuildReferenceKind.Task;
						return;
					case MSBuildKind.Parameter:
						rr.ReferenceKind = MSBuildReferenceKind.TaskParameter;
						return;
					case MSBuildKind.Property:
						rr.ReferenceKind = MSBuildReferenceKind.Property;
						return;
					default:
						rr.ReferenceKind = MSBuildReferenceKind.Keyword;
						return;
					}
				}

				base.VisitResolvedElement (element, resolved);
			}

			protected override void VisitResolvedAttribute (
				XElement element, XAttribute attribute,
				MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
			{
				var start = ConvertLocation (attribute.Region.Begin);
				bool inName = IsIn (start, attribute.Name.Name.Length);

				if (inName) {
					rr.ReferenceOffset = start;
					rr.ReferenceLength = attribute.Name.Name.Length;
					rr.Reference = attribute.Name.Name;
					switch (resolvedAttribute.AbstractKind) {
					case MSBuildKind.Metadata:
						rr.ReferenceKind = MSBuildReferenceKind.Metadata;
						rr.Reference = Tuple.Create (element.Name.Name, attribute.Name.Name);
						break;
					case MSBuildKind.Parameter:
						rr.ReferenceKind = MSBuildReferenceKind.TaskParameter;
						break;
					default:
						rr.ReferenceKind = MSBuildReferenceKind.Keyword;
						break;
					}
					return;
				}

				base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);
			}

			protected override void VisitValueExpression (
				XElement element, XAttribute attribute,
				MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute,
				ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
			{
				switch (node.Find (offset)) {
				case ExpressionItem ei:
					rr.ReferenceKind = MSBuildReferenceKind.Item;
					rr.ReferenceOffset = ei.NameOffset;
					rr.ReferenceLength = ei.Name.Length;
					rr.Reference = ei.Name;
					break;
				case ExpressionProperty ep:
					rr.ReferenceKind = MSBuildReferenceKind.Property;
					rr.ReferenceOffset = ep.NameOffset;
					rr.Reference = ep.Name;
					rr.ReferenceLength = ep.Name.Length;
					break;
				case ExpressionMetadata em:
					if (em.ItemName == null || offset >= em.MetadataNameOffset) {
						rr.ReferenceKind = MSBuildReferenceKind.Metadata;
						rr.ReferenceOffset = em.MetadataNameOffset;
						rr.Reference = Tuple.Create (em.GetItemName (), em.MetadataName);
						rr.ReferenceLength = em.MetadataName.Length;
					} else {
						rr.ReferenceKind = MSBuildReferenceKind.Item;
						rr.ReferenceOffset = em.ItemNameOffset;
						rr.Reference = em.ItemName;
						rr.ReferenceLength = em.ItemName.Length;
					}
					break;
				case ExpressionLiteral lit:
					kind = kind.GetScalarType ();
					if (lit.IsPure) {
						VisitPureLiteral (info, kind, lit);
					}
					switch (kind) {
					case MSBuildValueKind.File:
					case MSBuildValueKind.FileOrFolder:
					case MSBuildValueKind.ProjectFile:
					case MSBuildValueKind.TaskAssemblyFile:
						var pathNode = lit.Parent as Expression ?? (ExpressionNode)lit;
						var path = MSBuildNavigation.GetPathFromNode (pathNode, (MSBuildRootDocument)Document);
						if (path != null) {
							rr.ReferenceKind = MSBuildReferenceKind.FileOrFolder;
							rr.ReferenceOffset = path.Offset;
							rr.ReferenceLength = path.Length;
							rr.Reference = path.Paths;
						}
						break;
					}
					break;
				}
			}

			void VisitPureLiteral (ValueInfo info, MSBuildValueKind kind, ExpressionLiteral node)
			{
				rr.ReferenceOffset = node.Offset;
				rr.ReferenceLength = node.Value.Length;
				rr.Reference = node.Value;

				switch (kind) {
				case MSBuildValueKind.TargetName:
					rr.ReferenceKind = MSBuildReferenceKind.Target;
					return;
				case MSBuildValueKind.NuGetID:
					rr.ReferenceKind = MSBuildReferenceKind.NuGetID;
					return;
				case MSBuildValueKind.TargetFramework:
					rr.Reference = new FrameworkReference (null, null, node.Value, null);
					rr.ReferenceKind = MSBuildReferenceKind.TargetFramework;
					return;
				case MSBuildValueKind.TargetFrameworkIdentifier:
					rr.Reference = new FrameworkReference (node.Value, null, null, null);
					rr.ReferenceKind = MSBuildReferenceKind.TargetFramework;
					return;
				case MSBuildValueKind.TargetFrameworkVersion:
					rr.Reference = new FrameworkReference (null, node.Value, null, null);
					rr.ReferenceKind = MSBuildReferenceKind.TargetFramework;
					return;
				case MSBuildValueKind.TargetFrameworkProfile:
					rr.Reference = new FrameworkReference (null, null, null, node.Value);
					rr.ReferenceKind = MSBuildReferenceKind.TargetFramework;
					return;
				}

				IReadOnlyList<ConstantInfo> knownVals = info.Values ?? kind.GetSimpleValues (false);

				if (knownVals != null && knownVals.Count != 0) {
					foreach (var kv in knownVals) {
						if (string.Equals (kv.Name, node.Value, StringComparison.OrdinalIgnoreCase)) {
							rr.ReferenceKind = MSBuildReferenceKind.KnownValue;
							rr.Reference = kv;
							return;
						}
					}
				}
			}
		}
	}

	class MSBuildResolveResult
	{
		public XElement XElement;
		public XAttribute XAttribute;

		public MSBuildLanguageElement LanguageElement;
		public MSBuildLanguageAttribute LanguageAttribute;

		public string AttributeName => XAttribute?.Name.Name;
		public string ElementName => XElement?.Name.Name;
		public string ParentName => (XElement?.Parent as XElement)?.Name.Name;

		public MSBuildReferenceKind ReferenceKind;
		public int ReferenceOffset;
		public int ReferenceLength;
		public object Reference;
	}

	enum MSBuildReferenceKind
	{
		None,
		Item,
		Property,
		Metadata,
		Task,
		TaskParameter,
		Keyword,
		Target,
		KnownValue,
		NuGetID,
		TargetFramework,
		FileOrFolder
	}
}
