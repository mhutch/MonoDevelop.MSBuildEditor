// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
#nullable enable
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language
{
	static class MSBuildResolver
	{
		public static MSBuildResolveResult? Resolve (
			XmlSpineParser spineParser,
			ITextSource textSource,
			MSBuildDocument context,
			IFunctionTypeProvider functionTypeProvider,
			ILogger logger,
			CancellationToken cancellationToken = default)
		{
			int offset = spineParser.Position;

			if (!spineParser.TryAdvanceToNodeEndAndGetNodePath (textSource, out List<XObject>? nodePath, cancellationToken: cancellationToken)) {
				return null;
			}

			nodePath.ConnectParents ();

			//need to look up element by walking how the path, since at each level, if the parent has special children,
			//then that gives us information to identify the type of its children
			MSBuildElementSyntax? languageElement = null;
			MSBuildAttributeSyntax? languageAttribute = null;
			XElement? el = null;
			XAttribute? att = null;

			// todo: need to wind forward a bit further to get the whole value if cursor is right at the start
			foreach (var node in nodePath) {
				if (node is XAttribute xatt && xatt.Name.Prefix == null && xatt.Name.Name is string attName) {
					att = xatt;
					languageAttribute = languageElement?.GetAttribute (attName);
					break;
				}

				//if children of parent is known to be arbitrary data, don't go into it
				if (languageElement != null && languageElement.ValueKind == MSBuildValueKind.Data) {
					break;
				}

				//code completion is forgiving, all we care about best guess resolve for deepest child
				if (node is XElement xel && !xel.Name.HasPrefix && xel.Name.Name is string elName) {
					el = xel;
					languageElement = MSBuildElementSyntax.Get (elName, languageElement);
					if (languageElement != null)
						continue;
				}

				if (node is XText) {
					continue;
				}

				if (node is XClosingTag ct && ct == el?.ClosingTag) {
					continue;
				}

				languageElement = null;
			}

			if (languageElement == null || el is null) {
				return null;
			}

			var rr = new MSBuildMutableResolveResult {
				ElementSyntax = languageElement,
				AttributeSyntax = languageAttribute,
				Element = el,
				Attribute = att
			};

			var rv = new MSBuildResolveVisitor (context, textSource, logger, offset, rr, functionTypeProvider);

			try {
				rv.Run (el, languageElement, token: cancellationToken);
			} catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
				// callers always have to handle the possibility this returns null
				// so this means callers don't need to handle cancellation exceptions explicitly
				return null;
			}

			return rr.AsImmutable ();
		}

		// ************************
		// This is deeply coupled with MSBuildResolveResult, as it handles all casting back out from the untyped `Reference` object
		// ************************

		class MSBuildResolveVisitor : MSBuildDocumentVisitor
		{
			readonly int offset;
			readonly MSBuildMutableResolveResult rr;
			readonly IFunctionTypeProvider functionTypeProvider;

			public MSBuildResolveVisitor (MSBuildDocument document, ITextSource textSource, ILogger logger, int offset, MSBuildMutableResolveResult rr, IFunctionTypeProvider functionTypeProvider)
				: base (document, textSource, logger)
			{
				this.offset = offset;
				this.rr = rr;
				this.functionTypeProvider = functionTypeProvider;
			}

			bool IsIn (int start, int length) => offset >= start && offset <= (start + length);
			bool IsIn (TextSpan span) => span.ContainsOuter (offset);

			protected override void VisitResolvedElement (XElement element, MSBuildElementSyntax elementSyntax, ITypedSymbol elementSymbol)
			{
				var start = element.NameOffset;

				if (!element.Name.IsValid) {
					base.VisitResolvedElement (element, elementSyntax, elementSymbol);
					return;
				}

				if (!IsIn (element.NameSpan)) {
					if (element.ClosingTag is XClosingTag ct && IsIn (ct.NameSpan)) {
						start = ct.NameSpan.Start;
					} else {
						base.VisitResolvedElement (element, elementSyntax, elementSymbol);
						return;
					}
				}

				string elementName = element.Name.Name;
				rr.ReferenceOffset = start;
				rr.Reference = elementName;
				rr.ReferenceLength = elementName.Length;

				rr.ElementSyntax = elementSyntax;
				rr.ElementSymbol = elementSymbol;

				switch (elementSyntax.SyntaxKind) {
				case MSBuildSyntaxKind.Item:
				case MSBuildSyntaxKind.ItemDefinition:
					rr.ReferenceKind = MSBuildReferenceKind.Item;
					return;
				case MSBuildSyntaxKind.Metadata:
					rr.ReferenceKind = MSBuildReferenceKind.Metadata;
					rr.Reference = (element.ParentElement!.Name.Name, elementName);
					return;
				case MSBuildSyntaxKind.Task:
					rr.ReferenceKind = MSBuildReferenceKind.Task;
					return;
				case MSBuildSyntaxKind.Parameter:
					var taskName = element.ParentElement!.ParentElement!.Attributes.Get (MSBuildAttributeName.TaskName, true)?.Value;
					if (!string.IsNullOrEmpty (taskName)) {
						taskName = taskName.Substring (taskName.LastIndexOf ('.') + 1);
						rr.ReferenceKind = MSBuildReferenceKind.TaskParameter;
						rr.Reference = (taskName, elementName);
					}
					return;
				case MSBuildSyntaxKind.Property:
					rr.ReferenceKind = MSBuildReferenceKind.Property;
					return;
				default:
					if (!elementSyntax.IsAbstract) {
						rr.ReferenceKind = MSBuildReferenceKind.Keyword;
						rr.Reference = elementSyntax;
					}
					return;
				}
			}

			protected override void VisitResolvedAttribute (
				XElement element, XAttribute attribute,
				MSBuildElementSyntax elementSyntax, MSBuildAttributeSyntax attributeSyntax, ITypedSymbol elementSymbol, ITypedSymbol attributeSymbol)
			{
				if (!attribute.Span.Contains (offset)) {
					return;
				}

				rr.ElementSyntax = elementSyntax;
				rr.AttributeSyntax = attributeSyntax;
				rr.ElementSymbol = elementSymbol;
				rr.AttributeSymbol = attributeSymbol;

				if (attribute.Name.IsValid && attribute.NameSpan.Contains (offset)) {
					rr.ReferenceOffset = attribute.Span.Start;
					rr.ReferenceLength = attribute.Name.Name.Length;
					switch (attributeSyntax.AbstractKind) {
					case MSBuildSyntaxKind.Metadata:
						rr.ReferenceKind = MSBuildReferenceKind.Metadata;
						rr.Reference = (element.Name.Name, attribute.Name.Name);
						break;
					case MSBuildSyntaxKind.Parameter:
						rr.ReferenceKind = MSBuildReferenceKind.TaskParameter;
						rr.Reference = (element.Name.Name, attribute.Name.Name);
						break;
					default:
						if (!attributeSyntax.IsAbstract) {
							rr.ReferenceKind = MSBuildReferenceKind.Keyword;
							rr.Reference = attributeSyntax;
						}
						break;
					}
					return;
				}

				base.VisitResolvedAttribute (element, attribute, elementSyntax, attributeSyntax, elementSymbol, attributeSymbol);
			}

			protected override void VisitValue (
				XElement element, XAttribute? attribute,
				MSBuildElementSyntax elementSyntax, MSBuildAttributeSyntax? attributeSyntax,
				ITypedSymbol elementSymbol, ITypedSymbol? attributeSymbol,
				string expressionText, ExpressionNode node)
			{
				var valueSymbol = attributeSymbol ?? elementSymbol;

				var nodeAtOffset = node.Find (offset);
				switch (nodeAtOffset) {
				case ExpressionItemName ei:
					rr.ReferenceKind = MSBuildReferenceKind.Item;
					rr.ReferenceOffset = ei.Offset;
					rr.ReferenceLength = ei.Name.Length;
					rr.Reference = ei.Name;
					break;
				case ExpressionPropertyName propName:
					rr.ReferenceKind = MSBuildReferenceKind.Property;
					rr.ReferenceOffset = propName.Offset;
					rr.Reference = propName.Name;
					rr.ReferenceLength = propName.Length;
					break;
				case ExpressionMetadata em:
					if (em.ItemName == null || offset >= em.MetadataNameOffset) {
						rr.ReferenceKind = MSBuildReferenceKind.Metadata;
						rr.ReferenceOffset = em.MetadataNameOffset;
						rr.Reference = (em.GetItemName (), em.MetadataName);
						rr.ReferenceLength = em.MetadataName.Length;
					} else {
						rr.ReferenceKind = MSBuildReferenceKind.Item;
						rr.ReferenceOffset = em.ItemNameOffset;
						rr.Reference = em.ItemName;
						rr.ReferenceLength = em.ItemName.Length;
					}
					break;
				case ExpressionFunctionName name:
					rr.ReferenceOffset = name.Offset;
					rr.ReferenceLength = name.Name.Length;
					switch (name.Parent) {
					case ExpressionItemNode _:
						rr.ReferenceKind = MSBuildReferenceKind.ItemFunction;
						rr.Reference = name.Name;
						break;
					case ExpressionPropertyFunctionInvocation prop: {
						if (prop.Target is ExpressionClassReference classRef) {
							rr.ReferenceKind = MSBuildReferenceKind.StaticPropertyFunction;
							rr.Reference = (classRef.Name, name.Name);
						} else if (prop.Target is ExpressionPropertyNode propNode) {
							var type = functionTypeProvider?.ResolveType (propNode) ?? MSBuildValueKind.Unknown;
							rr.ReferenceKind = MSBuildReferenceKind.PropertyFunction;
							rr.Reference = (type, name.Name);
						}
						break;
					}
					case ExpressionConditionFunction _:
						rr.ReferenceKind = MSBuildReferenceKind.ConditionFunction;
						rr.Reference = name.Name;
						break;
					}
					break;
				case ExpressionClassReference cr:
					if (!string.IsNullOrEmpty (cr.Name)) {
						if (cr.Parent is ExpressionArgumentList) {
							rr.ReferenceKind = MSBuildReferenceKind.Enum;
						} else if (cr.Parent is ExpressionPropertyFunctionInvocation) {
							rr.ReferenceKind = MSBuildReferenceKind.ClassName;
						} else {
							break;
						}
						rr.ReferenceOffset = cr.Offset;
						rr.Reference = cr.Name;
						rr.ReferenceLength = cr.Length;
					}
					break;
				case ExpressionText lit:
					var kindWithoutModifiers = valueSymbol.ValueKindWithoutModifiers ();
					if (lit.IsPure) {
						VisitPureLiteral (element, valueSymbol, kindWithoutModifiers, lit);
						if (kindWithoutModifiers == MSBuildValueKind.TaskOutputParameterName) {
							rr.ReferenceKind = MSBuildReferenceKind.TaskParameter;
							rr.ReferenceOffset = lit.Offset;
							rr.ReferenceLength = lit.Value.Length;
							rr.Reference = (element.ParentElement!.Name.Name, lit.Value);
							break;
						}
					}
					switch (kindWithoutModifiers) {
					case MSBuildValueKind.File:
					case MSBuildValueKind.FileOrFolder:
					case MSBuildValueKind.ProjectFile:
					case MSBuildValueKind.TaskAssemblyFile:
						var pathNode = lit.Parent as ConcatExpression ?? (ExpressionNode)lit;
						var path = MSBuildNavigation.GetPathFromNode (pathNode, (MSBuildRootDocument)Document, Logger);
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

				if (rr.ReferenceKind != MSBuildReferenceKind.None) {
					rr.ElementSyntax = elementSyntax;
					rr.AttributeSyntax = attributeSyntax;
					rr.ElementSymbol = elementSymbol;
					rr.AttributeSymbol = attributeSymbol;
				}
			}

			void VisitPureLiteral (XElement element, ITypedSymbol valueSymbol, MSBuildValueKind kindWithoutModifiers, ExpressionText node)
			{
				string value = node.GetUnescapedValue (true, out int trimmedOffset, out int escapedLength);
				if (string.IsNullOrEmpty (value)) {
					return;
				}
				rr.ReferenceOffset = trimmedOffset;
				rr.ReferenceLength = escapedLength;
				rr.Reference = value;

				switch (kindWithoutModifiers) {
				case MSBuildValueKind.TaskOutputParameterName:
					rr.ReferenceKind = MSBuildReferenceKind.TaskParameter;
					return;
				case MSBuildValueKind.TargetName:
					rr.ReferenceKind = MSBuildReferenceKind.Target;
					return;
				case MSBuildValueKind.NuGetID:
					rr.ReferenceKind = MSBuildReferenceKind.NuGetID;
					return;
				case MSBuildValueKind.PropertyName:
					rr.ReferenceKind = MSBuildReferenceKind.Property;
					return;
				case MSBuildValueKind.ItemName:
					rr.ReferenceKind = MSBuildReferenceKind.Item;
					return;
				case MSBuildValueKind.TaskName:
					rr.ReferenceKind = MSBuildReferenceKind.Task;
					return;
				case MSBuildValueKind.TargetFramework:
					rr.ReferenceKind = MSBuildReferenceKind.TargetFramework;
					return;
				case MSBuildValueKind.TargetFrameworkIdentifier:
					rr.ReferenceKind = MSBuildReferenceKind.TargetFrameworkIdentifier;
					return;
				case MSBuildValueKind.TargetFrameworkVersion:
					rr.ReferenceKind = MSBuildReferenceKind.TargetFrameworkVersion;
					return;
				case MSBuildValueKind.TargetFrameworkProfile:
					rr.ReferenceKind = MSBuildReferenceKind.TargetFrameworkProfile;
					return;
				case MSBuildValueKind.MetadataName:
					//this is used for KeepMetadata/RemoveMetadata.
					//reasonable to resolve from first item in include.
					var itemName = MSBuildMetadataReferenceCollector.GetIncludeExpression (element)?
						.WithAllDescendants ()
						.OfType<ExpressionItemName> ()
						.FirstOrDefault ();
					if (itemName != null) {
						rr.Reference = (itemName.Name, value);
						rr.ReferenceKind = MSBuildReferenceKind.Metadata;
					}
					return;
				case MSBuildValueKind.Lcid:
					if (CultureHelper.TryGetLcidSymbol (value, out ITypedSymbol? lcidSymbol)) {
						rr.ReferenceKind = MSBuildReferenceKind.KnownValue;
						rr.Reference = lcidSymbol;
					}
					break;
				case MSBuildValueKind.Culture:
					if (CultureHelper.TryGetCultureSymbol (value, out ITypedSymbol? cultureSymbol)) {
						rr.ReferenceKind = MSBuildReferenceKind.KnownValue;
						rr.Reference = cultureSymbol;
					}
					return;
				}

				if (Document.GetSchemas (true).TryGetKnownValue (valueSymbol, value, out ITypedSymbol? knownValue, out _)) {
					rr.ReferenceKind = MSBuildReferenceKind.KnownValue;
					rr.Reference = knownValue;
					return;
				}
			}
		}

		/// <summary>
		/// Mutable version of <see cref="MSBuildResolveResult"/> for use during resolution.
		/// </summary>
		internal class MSBuildMutableResolveResult
		{
			public MSBuildReferenceKind ReferenceKind;
			public int ReferenceOffset;
			public int ReferenceLength;

			public object? Reference;

			public XElement? Element;
			public XAttribute? Attribute;

			public MSBuildElementSyntax? ElementSyntax;
			public MSBuildAttributeSyntax? AttributeSyntax;

			public ITypedSymbol? ElementSymbol;
			public ITypedSymbol? AttributeSymbol;

			public MSBuildResolveResult AsImmutable () => new (this);
		}
	}
}
