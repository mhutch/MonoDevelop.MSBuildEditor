// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language
{
	static class MSBuildResolver
	{
		public static MSBuildResolveResult Resolve (
			XmlSpineParser spineParser,
			ITextSource textSource,
			MSBuildDocument context,
			IFunctionTypeProvider functionTypeProvider,
			CancellationToken cancellationToken = default)
		{
			int offset = spineParser.Position;

			var nodePath = spineParser.AdvanceToNodeEndAndGetNodePath (textSource);
			nodePath.ConnectParents ();

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

				if (node is XText) {
					continue;
				}

				if (node is XClosingTag ct && ct == el.ClosingTag) {
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

			var rv = new MSBuildResolveVisitor (offset, rr, functionTypeProvider);

			try {
				rv.Run (el, languageElement, textSource, context, token: cancellationToken);
			} catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
				// callers always have to handle the possibility this returns null
				// so this means callers don't need to handle cancellation exceptions explciitly
				return null;
			}

			return rr;
		}

		class MSBuildResolveVisitor : MSBuildResolvingVisitor
		{
			int offset;
			readonly MSBuildResolveResult rr;
			readonly IFunctionTypeProvider functionTypeProvider;

			public MSBuildResolveVisitor (int offset, MSBuildResolveResult rr, IFunctionTypeProvider functionTypeProvider)
			{
				this.offset = offset;
				this.rr = rr;
				this.functionTypeProvider = functionTypeProvider;
			}

			bool IsIn (int start, int length) => offset >= start && offset <= (start + length);

			protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
			{
				var start = element.NameOffset;
				bool inName = element.IsNamed && IsIn (start, element.Name.Name.Length);
				if (inName) {
					rr.ReferenceOffset = start;
					rr.Reference = element.Name.Name;
					rr.ReferenceLength = element.Name.Name.Length;
					switch (resolved.SyntaxKind) {
					case MSBuildSyntaxKind.Item:
					case MSBuildSyntaxKind.ItemDefinition:
						rr.ReferenceKind = MSBuildReferenceKind.Item;
						return;
					case MSBuildSyntaxKind.Metadata:
						rr.ReferenceKind = MSBuildReferenceKind.Metadata;
						rr.Reference = (element.ParentElement.Name.Name, element.Name.Name);
						return;
					case MSBuildSyntaxKind.Task:
						rr.ReferenceKind = MSBuildReferenceKind.Task;
						return;
					case MSBuildSyntaxKind.Parameter:
						var taskName = element.ParentElement.ParentElement.Attributes.Get ("TaskName", true)?.Value;
						if (!string.IsNullOrEmpty (taskName)) {
							taskName = taskName.Substring (taskName.LastIndexOf ('.') + 1);
							rr.ReferenceKind = MSBuildReferenceKind.TaskParameter;
							rr.Reference = (taskName, element.Name.Name);
						}
						return;
					case MSBuildSyntaxKind.Property:
						rr.ReferenceKind = MSBuildReferenceKind.Property;
						return;
					default:
						if (!resolved.IsAbstract) {
							rr.ReferenceKind = MSBuildReferenceKind.Keyword;
							rr.Reference = resolved;
						}
						return;
					}
				}

				base.VisitResolvedElement (element, resolved);
			}

			protected override void VisitResolvedAttribute (
				XElement element, XAttribute attribute,
				MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
			{
				if (!attribute.Span.Contains (offset)) {
					return;
				}

				rr.LanguageAttribute = resolvedAttribute
					= Document.GetSchemas ().SpecializeAttribute (resolvedAttribute, element.Name.Name);

				bool inName = attribute.NameSpan.Contains (offset);

				if (inName) {
					rr.ReferenceOffset = attribute.Span.Start;
					rr.ReferenceLength = attribute.Name.Name.Length;
					switch (resolvedAttribute.AbstractKind) {
					case MSBuildSyntaxKind.Metadata:
						rr.ReferenceKind = MSBuildReferenceKind.Metadata;
						rr.Reference = (element.Name.Name, attribute.Name.Name);
						break;
					case MSBuildSyntaxKind.Parameter:
						rr.ReferenceKind = MSBuildReferenceKind.TaskParameter;
						rr.Reference = (element.Name.Name, attribute.Name.Name);
						break;
					default:
						if (!resolvedAttribute.IsAbstract) {
							rr.ReferenceKind = MSBuildReferenceKind.Keyword;
							rr.Reference = resolvedAttribute;
						}
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
					if (name.Parent is ExpressionItemNode item) {
						rr.ReferenceKind = MSBuildReferenceKind.ItemFunction;
						rr.Reference = name.Name;
					} else if (name.Parent is ExpressionPropertyFunctionInvocation prop) {
						if (prop.Target is ExpressionClassReference classRef) {
							rr.ReferenceKind = MSBuildReferenceKind.StaticPropertyFunction;
							rr.Reference = (classRef.Name, name.Name);
						} else {
							var type = functionTypeProvider?.ResolveType (prop.Target) ?? MSBuildValueKind.Unknown;
							rr.ReferenceKind = MSBuildReferenceKind.PropertyFunction;
							rr.Reference = (type, name.Name);
						}
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
					kind = kind.GetScalarType ();
					if (lit.IsPure) {
						VisitPureLiteral (element, info, kind, lit);
						if (kind == MSBuildValueKind.TaskOutputParameterName) {
							rr.ReferenceKind = MSBuildReferenceKind.TaskParameter;
							rr.ReferenceOffset = lit.Offset;
							rr.ReferenceLength = lit.Value.Length;
							rr.Reference = (element.ParentElement.Name.Name, lit.Value);
							break;
						}
					}
					switch (kind) {
					case MSBuildValueKind.File:
					case MSBuildValueKind.FileOrFolder:
					case MSBuildValueKind.ProjectFile:
					case MSBuildValueKind.TaskAssemblyFile:
						var pathNode = lit.Parent as ConcatExpression ?? (ExpressionNode)lit;
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

			void VisitPureLiteral (XElement element, ValueInfo info, MSBuildValueKind kind, ExpressionText node)
			{
				string value = node.GetUnescapedValue ();
				rr.ReferenceOffset = node.Offset;
				rr.ReferenceLength = node.Value.Length;
				rr.Reference = value;

				switch (kind) {
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
					var itemName = MSBuildMetadataReferenceCollector.GetIncludeExpression (element)
						.WithAllDescendants ()
						.OfType<ExpressionItemName> ()
						.FirstOrDefault ();
					if (itemName != null) {
						rr.Reference = (itemName.Name, value);
						rr.ReferenceKind = MSBuildReferenceKind.Metadata;
					}
					return;
				}

				IReadOnlyList<ConstantInfo> knownVals = info.Values ?? kind.GetSimpleValues (false);

				if (knownVals != null && knownVals.Count != 0) {
					foreach (var kv in knownVals) {
						if (string.Equals (kv.Name, value, StringComparison.OrdinalIgnoreCase)) {
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

		public (string itemName, string metaName) ReferenceAsMetadata => (ValueTuple<string, string>)Reference;
		public (string taskName, string paramName) ReferenceAsTaskParameter => (ValueTuple<string, string>)Reference;
		public (MSBuildValueKind type, string functionName) ReferenceAsPropertyFunction => (ValueTuple<MSBuildValueKind, string>)Reference;
		public (string className, string functionName) ReferenceAsStaticPropertyFunction => (ValueTuple<string, string>)Reference;

		public string GetReferenceName ()
		{
			switch (ReferenceKind) {
			case MSBuildReferenceKind.TaskParameter:
				return ReferenceAsTaskParameter.paramName;
			case MSBuildReferenceKind.Metadata:
				return ReferenceAsMetadata.metaName;
			case MSBuildReferenceKind.PropertyFunction:
				return ReferenceAsPropertyFunction.functionName;
			case MSBuildReferenceKind.StaticPropertyFunction:
				return ReferenceAsStaticPropertyFunction.functionName;
			}
			return Reference is BaseInfo info ? info.Name : (string)Reference;
		}
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
		TargetFrameworkIdentifier,
		TargetFrameworkVersion,
		TargetFrameworkProfile,
		FileOrFolder,
		ItemFunction,
		PropertyFunction,
		StaticPropertyFunction,
		ClassName,
		Enum
	}
}
