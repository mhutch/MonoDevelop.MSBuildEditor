// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuildEditor.Language
{
	static class MSBuildResolver
	{
		public static MSBuildResolveResult Resolve (XmlParser parser, IReadonlyTextDocument document, MSBuildResolveContext context)
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
					rr.ReferenceName = element.Name.Name;
					switch (resolved.Kind) {
					case MSBuildKind.Item:
					case MSBuildKind.ItemDefinition:
						rr.ReferenceKind = MSBuildReferenceKind.Item;
						return;
					case MSBuildKind.Metadata:
						rr.ReferenceKind = MSBuildReferenceKind.Metadata;
						rr.ReferenceItemName = element.ParentElement ().Name.Name;
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

			protected override void VisitResolvedAttribute (XElement element, XAttribute attribute, MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
			{
				var start = ConvertLocation (attribute.Region.Begin);
				bool inName = IsIn (start, attribute.Name.Name.Length);

				if (inName) {
					rr.ReferenceOffset = start;
					rr.ReferenceName = attribute.Name.Name;
					switch (resolvedAttribute.AbstractKind) {
					case MSBuildKind.Metadata:
						rr.ReferenceKind = MSBuildReferenceKind.Metadata;
						rr.ReferenceItemName = element.Name.Name;
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

			protected override void VisitValue(ValueInfo info, string value, int offset)
			{
				var kind = MSBuildCompletionExtensions.InferValueKindIfUnknown (info);
				var options = kind.GetExpressionOptions () | ExpressionOptions.ItemsMetadataAndLists;

				var node = ExpressionParser.Parse (value, options, offset);
				VisitValueExpression (info, kind, node);
			}

			protected override void VisitValueExpression (ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
			{
				switch (node.Find (offset)) {
				case ExpressionItem ei:
					rr.ReferenceKind = MSBuildReferenceKind.Item;
					rr.ReferenceOffset = ei.NameOffset;
					rr.ReferenceName = ei.Name;
					break;
				case ExpressionProperty ep:
					rr.ReferenceKind = MSBuildReferenceKind.Property;
					rr.ReferenceOffset = ep.NameOffset;
					rr.ReferenceName = ep.Name;
					break;
				case ExpressionMetadata em:
					if (em.ItemName == null || offset >= em.MetadataNameOffset) {
						rr.ReferenceKind = MSBuildReferenceKind.Metadata;
						rr.ReferenceOffset = em.MetadataNameOffset;
						rr.ReferenceName = em.MetadataName;
						rr.ReferenceItemName = em.GetItemName ();
					} else {
						rr.ReferenceKind = MSBuildReferenceKind.Item;
						rr.ReferenceOffset = em.ItemNameOffset;
						rr.ReferenceName = em.ItemName;
					}
					break;
				case ExpressionLiteral lit:
					if (lit.Parent == null || lit.Parent is ExpressionList) {
						VisitPureLiteral (kind.GetScalarType (), lit);
					}
					break;
				}
			}

			void VisitPureLiteral (MSBuildValueKind kind, ExpressionLiteral node)
			{
				rr.ReferenceOffset = node.Offset;
				rr.ReferenceName = node.Value;

				if (kind == MSBuildValueKind.TargetName) {
					rr.ReferenceKind = MSBuildReferenceKind.Target;
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
		public string ReferenceName;
		public string ReferenceItemName;

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
		Value
	}
}
