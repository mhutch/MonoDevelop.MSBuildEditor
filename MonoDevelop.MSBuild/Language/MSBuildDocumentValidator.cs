// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Microsoft.Extensions.Logging;

using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language
{
	partial class MSBuildDocumentValidator : MSBuildResolvingVisitor
	{
		public MSBuildDocumentValidator (MSBuildDocument document, ITextSource textSource, ILogger logger) : base (document, textSource, logger)
		{
		}

		protected override void VisitUnknownElement (XElement element)
		{
			Document.Diagnostics.Add (CoreDiagnostics.UnknownElement, element.Span, element.Name.FullName);
			base.VisitUnknownElement (element);
		}

		protected override void VisitUnknownAttribute (XElement element, XAttribute attribute)
		{
			Document.Diagnostics.Add (CoreDiagnostics.UnknownAttribute, attribute.Span, attribute.Name.FullName);
			base.VisitUnknownAttribute (element, attribute);
		}

		protected override void VisitResolvedElement (XElement element, MSBuildElementSyntax resolved)
		{
			try {
				ValidateResolvedElement (element, resolved);
				//don't validate children of incomplete elements
				if (element.IsComplete) {
					base.VisitResolvedElement (element, resolved);
				}
			} catch (Exception ex) when (!(ex is OperationCanceledException && CancellationToken.IsCancellationRequested)) {
				Document.Diagnostics.Add (CoreDiagnostics.InternalError, element.NameSpan, ex.Message);
				LogInternalError (Logger, ex);
			}
		}

		void ValidateResolvedElement (XElement element, MSBuildElementSyntax resolved)
		{
			foreach (var rat in resolved.Attributes) {
				if (rat.Required && !rat.IsAbstract) {
					var xat = element.Attributes.Get (rat.Name, true);
					if (xat == null) {
						Document.Diagnostics.Add (CoreDiagnostics.MissingRequiredAttribute, element.NameSpan, element.Name, rat.Name);
					}
				}
			}

			TextSpan[] GetNameSpans (XElement el) => (el.ClosingTag is XClosingTag ct)
				? new[] { element.NameSpan, new TextSpan (ct.Span.Start + 2, ct.Name.Length) }
				: new[] { element.NameSpan };

			switch (resolved.SyntaxKind) {
			case MSBuildSyntaxKind.Project:
				if (!IsPropsFile) {
					ValidateProjectHasTarget (element);
				}
				break;
			case MSBuildSyntaxKind.OnError:
				ValidateOnErrorOnlyFollowedByOnError (element);
				break;
			case MSBuildSyntaxKind.Otherwise:
				ValidateOtherwiseIsLastElement (element);
				break;
			case MSBuildSyntaxKind.Output:
				ValidateOutputHasPropertyOrItemName (element);
				break;
			case MSBuildSyntaxKind.UsingTask:
				ValidateUsingTaskHasAssembly (element);
				break;
			case MSBuildSyntaxKind.Import:
				ValidateImportOnlyHasVersionIfHasSdk (element);
				break;
			case MSBuildSyntaxKind.Item:
				ValidateItemAttributes (resolved, element);
				if (!IsItemUsed (element.Name.Name, ReferenceUsage.Read)) {
					Document.Diagnostics.Add (
						CoreDiagnostics.UnreadItem,
						element.NameSpan,
						ImmutableDictionary<string,object>.Empty
							.Add ("Name", element.Name.Name)
							.Add ("Spans", GetNameSpans (element)),
						element.Name.Name
					);
				}
				break;
			case MSBuildSyntaxKind.Task:
				ValidateTaskParameters (resolved, element);
				break;
			case MSBuildSyntaxKind.Property:
				if (!IsPropertyUsed (element.Name.Name, ReferenceUsage.Read)) {
					Document.Diagnostics.Add (
						CoreDiagnostics.UnreadProperty,
						element.NameSpan,
						ImmutableDictionary<string, object>.Empty
							.Add ("Name", element.Name.Name)
							.Add ("Spans", GetNameSpans (element)),
						element.Name.Name
					);
				}
				break;
			case MSBuildSyntaxKind.Metadata:
				if ((element.Parent as XElement)?.Name.Name is string metaItem
					&& !IsMetadataUsed (metaItem, element.Name.Name, ReferenceUsage.Read))
				{
					Document.Diagnostics.Add (
						CoreDiagnostics.UnreadMetadata,
						element.NameSpan,
						ImmutableDictionary<string, object>.Empty
							.Add ("ItemName", metaItem)
							.Add ("Name", element.Name.Name)
							.Add ("Spans", GetNameSpans (element)),
						metaItem, element.Name.Name
					);
				}
				break;
			}

			if (resolved.ValueKind == MSBuildValueKind.Nothing) {
				foreach (var txt in element.Nodes.OfType<XText> ()) {
					Document.Diagnostics.Add (CoreDiagnostics.UnexpectedText, txt.Span, element.Name.Name);
				}
			}
		}

		void CheckDeprecated (IDeprecatable info, INamedXObject namedObj)
		{
			if (info.IsDeprecated) {
				if (string.IsNullOrEmpty (info.DeprecationMessage)) {
					Document.Diagnostics.Add (
						CoreDiagnostics.Deprecated,
						namedObj.NameSpan,
						DescriptionFormatter.GetKindNoun (info),
					info.Name);
				} else {
					Document.Diagnostics.Add (
						CoreDiagnostics.DeprecatedWithMessage,
						namedObj.NameSpan,
						DescriptionFormatter.GetKindNoun (info),
						info.Name,
						info.DeprecationMessage
					);
				}
			}
		}

		void ValidateProjectHasTarget (XElement element)
		{
			if (element.Attributes.Get ("Sdk", true) != null) {
				return;
			}

			foreach (var child in element.Nodes) {
				if (child is XElement projectChild && !projectChild.Name.HasPrefix) {
					switch (projectChild.Name.Name.ToLower ()) {
					case "target":
					case "import":
						return;
					}
				}
			}

			Document.Diagnostics.Add (CoreDiagnostics.NoTargets, element.NameSpan);
		}

		void ValidateOnErrorOnlyFollowedByOnError (XElement element)
		{
			var nextSibling = element.GetNextSiblingElement ();
			if (nextSibling != null && !nextSibling.NameEquals ("OnError", true)) {
				Document.Diagnostics.Add (CoreDiagnostics.OnErrorMustBeLastInTarget, element.GetNextSiblingElement ().NameSpan);
			}
		}

		void ValidateOtherwiseIsLastElement (XElement element)
		{
			if (element.GetNextSiblingElement () != null) {
				Document.Diagnostics.Add (CoreDiagnostics.OtherwiseMustBeLastInChoose, element.GetNextSiblingElement ().NameSpan);
			}
		}

		void ValidateOutputHasPropertyOrItemName (XElement element)
		{
			bool foundItemOrPropertyName = false;
			foreach (var att in element.Attributes) {
				if (att.NameEquals ("ItemName", true) || att.NameEquals ("PropertyName", true)) {
					foundItemOrPropertyName = true;
					break;
				}
			}
			if (!foundItemOrPropertyName) {
				Document.Diagnostics.Add (CoreDiagnostics.OutputMustHavePropertyOrItemName, element.NameSpan);
			}
		}

		void ValidateUsingTaskHasAssembly (XElement element)
		{
			XAttribute taskFactoryAtt = null;
			XAttribute asmNameAtt = null;
			XAttribute asmFileAtt = null;

			foreach (var att in element.Attributes) {
				switch (att.Name.Name.ToLowerInvariant ()) {
				case "assemblyfile":
					asmFileAtt = att;
					break;
				case "assemblyname":
					asmNameAtt = att;
					break;
				case "taskfactory":
					taskFactoryAtt = att;
					break;
				}
			}

			if (asmNameAtt == null && asmFileAtt == null) {
				Document.Diagnostics.Add (CoreDiagnostics.UsingTaskMustHaveAssembly, element.NameSpan);
			} else if (taskFactoryAtt != null && asmNameAtt != null) {
				Document.Diagnostics.Add (CoreDiagnostics.TaskFactoryCannotHaveAssemblyName, asmNameAtt.NameSpan);
			} else if (taskFactoryAtt != null && asmFileAtt == null) {
				Document.Diagnostics.Add (CoreDiagnostics.TaskFactoryMustHaveAssemblyFile, element.NameSpan);
			} else if (asmNameAtt != null && asmFileAtt != null) {
				Document.Diagnostics.Add (CoreDiagnostics.TaskFactoryMustHaveOneAssemblyOnly, element.NameSpan);
			}

			XElement parameterGroup = null, taskBody = null;
			foreach (var child in element.Elements) {
				if (child.NameEquals ("ParameterGroup", true)) {
					if (parameterGroup != null) {
						Document.Diagnostics.Add (CoreDiagnostics.OneParameterGroup, child.NameSpan);
					}
					parameterGroup = child;
				}
				if (child.NameEquals ("Task", true)) {
					if (taskBody != null) {
						Document.Diagnostics.Add (CoreDiagnostics.OneTaskBody, child.NameSpan);
					}
					taskBody = child;
				}
			}

			if (taskFactoryAtt == null) {
				if (taskBody != null) {
					Document.Diagnostics.Add (CoreDiagnostics.TaskBodyMustHaveFactory, taskBody.NameSpan);
				} else if (parameterGroup != null) {
					Document.Diagnostics.Add (CoreDiagnostics.ParameterGroupMustHaveFactory, parameterGroup.NameSpan);
				}
			} else {
				if (taskBody == null) {
					Document.Diagnostics.Add (CoreDiagnostics.TaskFactoryMustHaveBody, element.NameSpan);
				}

				if (taskBody != null) {
					var taskFactoryName = taskFactoryAtt.Value?.ToLowerInvariant ();
					switch (taskFactoryName) {
					case "codetaskfactory":
						if (string.Equals (asmFileAtt?.Value, "$(RoslynCodeTaskFactory)")) {
							goto case "roslyncodetaskfactory";
						}
						break;
					case "roslyncodetaskfactory":
						ValidateRoslynCodeTaskFactory (element, taskBody, parameterGroup);
						break;
					default:
						Document.Diagnostics.Add (CoreDiagnostics.UnknownTaskFactory, element.NameSpan, taskFactoryName);
						break;
					}
				}
			}

		}

		void ValidateRoslynCodeTaskFactory (XElement usingTask, XElement taskBody, XElement parameterGroup)
		{
			var code = taskBody.Elements.FirstOrDefault (f => string.Equals (f.Name.Name, "code", StringComparison.OrdinalIgnoreCase));
			if (code == null) {
				Document.Diagnostics.Add (CoreDiagnostics.RoslynCodeTaskFactoryRequiresCodeElement, taskBody.NameSpan);
				return;
			}
			var typeAtt = code.Attributes.Get ("Type", true);
			var sourceAtt = code.Attributes.Get ("Source", true);
			if (sourceAtt != null || string.Equals (typeAtt?.Value, "Class", StringComparison.OrdinalIgnoreCase)) {
				if (parameterGroup != null) {
					Document.Diagnostics.Add (CoreDiagnostics.RoslynCodeTaskFactoryWithClassIgnoresParameterGroup, parameterGroup.NameSpan);
				}
			}
		}

		void ValidateImportOnlyHasVersionIfHasSdk (XElement element)
		{
			if (element.Attributes.Get ("Sdk", true) != null) {
				return;
			}

			foreach (var att in element.Attributes) {
				if (att.NameEquals ("Version", true)) {
					Document.Diagnostics.Add (CoreDiagnostics.ImportVersionRequiresSdk, att.NameSpan);
				}
				if (att.NameEquals ("MinVersion", true)) {
					Document.Diagnostics.Add (CoreDiagnostics.ImportMinVersionRequiresSdk, att.NameSpan);
				}
			}
		}

		void ValidateItemAttributes (MSBuildElementSyntax resolved, XElement element)
		{
			bool isInTarget = resolved.IsInTarget (element);
			bool hasInclude = false, hasUpdate = false, hasRemove = false;
			foreach (var att in element.Attributes) {
				hasInclude |= att.NameEquals ("Include", true);
				hasRemove |= att.NameEquals ("Remove", true);
				if (att.NameEquals ("Update", true)) {
					hasUpdate = true;
					if (isInTarget) {
						Document.Diagnostics.Add (CoreDiagnostics.ItemAttributeNotValidInTarget, att.NameSpan, att.Name.Name);
					}
				}
				if (att.NameEquals ("KeepMetadata", true) || att.NameEquals ("RemoveMetadata", true) || att.NameEquals ("KeepDuplicates", true)) {
					if (!isInTarget) {
						Document.Diagnostics.Add (CoreDiagnostics.ItemAttributeOnlyValidInTarget, att.NameSpan, att.Name.Name);
					}
				}
			}

			if (!hasInclude && !hasRemove && !hasUpdate && !isInTarget) {
				Document.Diagnostics.Add (CoreDiagnostics.ItemMustHaveInclude, element.NameSpan);
			}
		}

		void ValidateTaskParameters (MSBuildElementSyntax resolvedElement, XElement element)
		{
			var info = Document.GetSchemas ().GetTask (element.Name.Name);
			if (info.IsInferred) {
				Document.Diagnostics.Add (CoreDiagnostics.TaskNotDefined, element.NameSpan, element.Name.Name);
				return;
			}

			var required = new HashSet<string> ();
			foreach (var p in info.Parameters) {
				if (p.Value.IsRequired) {
					required.Add (p.Key);
				}
			}

			foreach (var att in element.Attributes) {
				if (!resolvedElement.GetAttribute (att.Name.Name).IsAbstract) {
					continue;
				}
				if (!info.Parameters.TryGetValue (att.Name.Name, out TaskParameterInfo pi)) {
					Document.Diagnostics.Add (CoreDiagnostics.UnknownTaskParameter, att.NameSpan, element.Name.Name, att.Name.Name);
					continue;
				}
				if (pi.IsRequired) {
					required.Remove (pi.Name);
					if (string.IsNullOrWhiteSpace (att.Value)) {
						Document.Diagnostics.Add (CoreDiagnostics.EmptyRequiredTaskParameter, att.NameSpan, element.Name.Name, att.Name.Name);
					}
				}
			}

			foreach (var r in required) {
				Document.Diagnostics.Add (CoreDiagnostics.MissingRequiredTaskParameter, element.NameSpan, element.Name.Name, r);
			}

			foreach (var child in element.Elements) {
				if (child.NameEquals ("Output", true)) {
					var paramNameAtt = child.Attributes.Get ("TaskParameter", true);
					var paramName = paramNameAtt?.Value;
					if (string.IsNullOrEmpty (paramName)) {
						continue;
					}
					if (!info.Parameters.TryGetValue (paramName, out TaskParameterInfo pi)) {
						Document.Diagnostics.Add (CoreDiagnostics.UnknownTaskParameter, paramNameAtt.ValueSpan, element.Name.Name, paramName);
						continue;
					}
					if (!pi.IsOutput) {
						Document.Diagnostics.Add (CoreDiagnostics.NonOutputTaskParameter, paramNameAtt.ValueSpan, element.Name.Name, paramName);
						continue;
					}
				}
			}
		}

		protected override void VisitResolvedAttribute (XElement element, XAttribute attribute, MSBuildElementSyntax resolvedElement, MSBuildAttributeSyntax resolvedAttribute)
		{
			if (resolvedAttribute.SyntaxKind == MSBuildSyntaxKind.Item_Metadata) {
				if (!IsMetadataUsed (element.Name.Name, attribute.Name.Name, ReferenceUsage.Read)) {
					Document.Diagnostics.Add (
						CoreDiagnostics.UnreadMetadata,
						attribute.NameSpan,
						ImmutableDictionary<string, object>.Empty
							.Add ("ItemName", element.Name.Name)
							.Add ("Name", attribute.Name.Name)
							.Add ("Spans", new[] { attribute.NameSpan }),
						element.Name.Name, attribute.Name.Name
					);
				}
			}
			ValidateAttribute (element, attribute, resolvedElement, resolvedAttribute);
			base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);
		}

		void ValidateAttribute (XElement element, XAttribute attribute, MSBuildElementSyntax resolvedElement, MSBuildAttributeSyntax resolvedAttribute)
		{
			if (string.IsNullOrWhiteSpace (attribute.Value)) {
				if (resolvedAttribute.Required) {
					Document.Diagnostics.Add (CoreDiagnostics.RequiredAttributeEmpty, attribute.NameSpan, attribute.Name);
				} else {
					Document.Diagnostics.Add (CoreDiagnostics.AttributeEmpty, attribute.NameSpan, attribute.Name);
				}
				return;
			}
		}

		protected override void VisitValue (
			XElement element, XAttribute attribute,
			MSBuildElementSyntax resolvedElement, MSBuildAttributeSyntax resolvedAttribute,
			ITypedSymbol valueDescriptor, string value, int offset)
		{
			if (!IsTargetsFile && !IsPropsFile && valueDescriptor is IHasDefaultValue hasDefault) {
				if (hasDefault.DefaultValue != null && string.Equals (hasDefault.DefaultValue, value, StringComparison.OrdinalIgnoreCase)) {
					Document.Diagnostics.Add (
						CoreDiagnostics.HasDefaultValue, attribute?.Span ?? element.OuterSpan,
						ImmutableDictionary<string,object>.Empty.Add ("Info", valueDescriptor),
						DescriptionFormatter.GetKindNoun (valueDescriptor), valueDescriptor.Name, hasDefault.DefaultValue);
				}
			}


			if (valueDescriptor is IDeprecatable deprecatable) {
				CheckDeprecated (deprecatable, (INamedXObject)attribute ?? element);
			}

			// we skip calling base, and instead parse the expression with more options enabled
			// so that we can warn if the user is doing something likely invalid
			var kind = MSBuildCompletionExtensions.InferValueKindIfUnknown (valueDescriptor);
			var options = kind.GetExpressionOptions () | ExpressionOptions.ItemsMetadataAndLists;

			var node = ExpressionParser.Parse (value, options, offset);
			VisitValueExpression (element, attribute, resolvedElement, resolvedAttribute, valueDescriptor, kind, node);
		}

		protected override void VisitValueExpression (
			XElement element, XAttribute attribute,
			MSBuildElementSyntax resolvedElement, MSBuildAttributeSyntax resolvedAttribute,
			ITypedSymbol info, MSBuildValueKind kind, ExpressionNode node)
		{
			bool allowExpressions = kind.AllowsExpressions ();
			bool allowLists = kind.AllowsLists (MSBuildValueKind.ListSemicolonOrComma);

			if (node is ListExpression list) {
				if (!allowLists) {
					Document.Diagnostics.Add (
					CoreDiagnostics.UnexpectedList,
					new TextSpan (list.Nodes[0].End, list.End - list.Nodes[0].End),
					ImmutableDictionary<string, object>.Empty.Add ("Name", info.Name),
					DescriptionFormatter.GetKindNoun (info),
					info.Name);
				}
				if (!allowExpressions) {
					var expr = list.Nodes.FirstOrDefault (n => !(n is ExpressionText));
					if (expr != null) {
						AddExpressionWarning (expr);
					}
				}
			} else if (node is ExpressionText lit) {
				VisitPureLiteral (info, kind, lit.GetUnescapedValue (), lit.Offset);
			} else {
				if (!allowExpressions) {
					AddExpressionWarning (node);
				}
			}

			foreach (var n in node.WithAllDescendants ()) {
				switch (n) {
				case ExpressionError err:
					var (desc, args) = CoreDiagnostics.GetExpressionError (err, info);
					Document.Diagnostics.Add (desc, new TextSpan (err.Offset, Math.Max (1, err.Length)), args);
					break;
				case ExpressionMetadata meta:
					var metaItem = meta.GetItemName ();
					if (!string.IsNullOrEmpty (metaItem) && !IsMetadataUsed (metaItem, meta.MetadataName, ReferenceUsage.Write)) {
						Document.Diagnostics.Add (
							CoreDiagnostics.UnwrittenMetadata,
							meta.Span,
							ImmutableDictionary<string, object>.Empty
								.Add ("ItemName", metaItem)
								.Add ("Name", meta.MetadataName)
								.Add ("Spans", new [] { new TextSpan (meta.MetadataNameOffset, meta.MetadataName.Length) }),
							metaItem, meta.MetadataName
						);
					}
					break;
				case ExpressionPropertyName prop:
					if (!IsPropertyUsed (prop.Name, ReferenceUsage.Write)) {
						AddFixableError (CoreDiagnostics.UnwrittenProperty, prop.Name, prop.Span, prop.Name);
					}
					break;
				case ExpressionItemName item:
					if (!IsItemUsed (item.Name, ReferenceUsage.Write)) {
						AddFixableError (CoreDiagnostics.UnwrittenItem, item.Name, item.Span, item.Name);
					}
					//TODO: deprecation squiggles in expressions
					break;
				}
			}

			void AddExpressionWarning (ExpressionNode n)
				=> Document.Diagnostics.Add (CoreDiagnostics.UnexpectedExpression,
				new TextSpan (n.Offset, n.Length),
				DescriptionFormatter.GetKindNoun (info),
				info.Name);

			// errors expected to be fixed by ChangeMisspelledNameFixProvider
			// captures the information needed by the fixer
			void AddFixableError (MSBuildDiagnosticDescriptor d, string symbolName, TextSpan symbolSpan, params object[] args)
			{
				Document.Diagnostics.Add (
					d,
					symbolSpan,
					ImmutableDictionary<string, object>.Empty
						.Add ("Name", symbolName)
					);
			}
		}

		//note: the value is unescaped, so offsets within it are not valid
		void VisitPureLiteral (ITypedSymbol info, MSBuildValueKind kind, string value, int offset)
		{
			if (info.CustomType != null && info.CustomType.AllowUnknownValues) {
				return;
			}

			var knownVals = (IReadOnlyList<ISymbol>)info.CustomType?.Values ?? kind.GetSimpleValues (false);

			if (knownVals != null && knownVals.Count != 0) {
				foreach (var kv in knownVals) {
					if (string.Equals (kv.Name, value, StringComparison.OrdinalIgnoreCase)) {
						return;
					}
				}
				AddFixableError (CoreDiagnostics.UnknownValue, DescriptionFormatter.GetKindNoun (info), info.Name, value);

				return;
			}

			switch (kind) {
			case MSBuildValueKind.Guid:
			case MSBuildValueKind.ProjectKindGuid:
				if (!Guid.TryParseExact (value, "B", out _)) {
					AddErrorWithArgs (CoreDiagnostics.InvalidGuid, value);
				}
				break;
			case MSBuildValueKind.Int:
				if (!long.TryParse (value, out _)) {
					AddErrorWithArgs (CoreDiagnostics.InvalidInteger, value);
				}
				break;
			case MSBuildValueKind.Bool:
				if (!bool.TryParse (value, out _)) {
					AddFixableError (CoreDiagnostics.InvalidBool, value);
				}
				break;
			case MSBuildValueKind.Url:
				if (!Uri.TryCreate (value, UriKind.Absolute, out _)) {
					AddErrorWithArgs (CoreDiagnostics.InvalidUrl, value);
				}
				break;
			case MSBuildValueKind.Version:
				if (!Version.TryParse (value, out _)) {
					AddErrorWithArgs (CoreDiagnostics.InvalidVersion, value);
				}
				break;
			/*
			 * FIXME: these won't work as-is, as inference will add them to the schema
			case MSBuildValueKind.TargetName:
				if (Document.GetSchemas ().GetTarget (value) == null) {
					AddErrorWithArgs (CoreDiagnostics.UndefinedTarget, value);
				}
				break;
			case MSBuildValueKind.PropertyName:
				if (Document.GetSchemas ().GetProperty (value) == null) {
					AddErrorWithArgs (CoreDiagnostics.UnknownProperty, value);
				}
				break;
			case MSBuildValueKind.ItemName:
				if (Document.GetSchemas ().GetItem (value) == null) {
					AddErrorWithArgs (CoreDiagnostics.UnknownProperty, value);
				}
				break;
				*/
			case MSBuildValueKind.Lcid:
				if (int.TryParse (value, out int lcid) && lcid > 0) {
					try {
						CultureInfo.GetCultureInfo (lcid);
					} catch (CultureNotFoundException) {
						AddErrorWithArgs (CoreDiagnostics.UnknownLcid, value);
					}
				} else {
					AddErrorWithArgs (CoreDiagnostics.InvalidLcid, value);
				}
				break;
			case MSBuildValueKind.Culture:
				static bool IsAsciiLetter (char c) => (c >= 48 && c <= 57) || (c >= 65 && c <= 90);
				static bool IsValidCultureName (string name) =>
					name.Length >= 2 && IsAsciiLetter (name[0]) && IsAsciiLetter (name[1])
					&& (name.Length == 2 || (name.Length == 5 && name[2] == '-' && IsAsciiLetter (name[3]) && IsAsciiLetter (name[4])));

				if (IsValidCultureName (value)) {
					try {
						CultureInfo.GetCultureInfo(value);
					} catch (CultureNotFoundException) {
						AddErrorWithArgs (CoreDiagnostics.UnknownCulture, value);
					}
				} else {
					AddErrorWithArgs (CoreDiagnostics.InvalidCulture, value);
				}
				break;
			case MSBuildValueKind.TargetFramework:
				if (!FrameworkInfoProvider.Instance.IsFrameworkShortNameValid (value)) {
					AddErrorWithArgs (CoreDiagnostics.UnknownTargetFramework, value);
				}
				break;
			case MSBuildValueKind.TargetFrameworkIdentifier:
				if (!FrameworkInfoProvider.Instance.IsFrameworkIdentifierValid (value)) {
					AddErrorWithArgs (CoreDiagnostics.UnknownTargetFrameworkIdentifier, value);
				}
				break;
			case MSBuildValueKind.TargetFrameworkVersion: {
					if (!Version.TryParse (value.TrimStart ('v', 'V'), out Version fxv)) {
						AddErrorWithArgs (CoreDiagnostics.InvalidVersion, value);
						break;
					}
					fxv = new Version (Math.Max (fxv.Major, 0), Math.Max (fxv.Minor, 0), Math.Max (fxv.Revision, 0), Math.Max (fxv.Build, 0));
					if (Document is MSBuildRootDocument d && d.Frameworks.Count > 0) {
						bool foundMatch = false;
						foreach (var fx in d.Frameworks) {
							if (FrameworkInfoProvider.AreVersionsEquivalent (fx.Version, fxv) && FrameworkInfoProvider.Instance.IsFrameworkVersionValid (fx.Framework, fxv)) {
								foundMatch = true;
							}
						}
						if (!foundMatch) {
							AddErrorWithArgs (CoreDiagnostics.UnknownTargetFrameworkVersion, value, d.Frameworks[0].Framework);
						}
					}
					break;
				}
			case MSBuildValueKind.TargetFrameworkProfile: {
					if (Document is MSBuildRootDocument d && d.Frameworks.Count > 0) {
						bool foundMatch = false;
						foreach (var fx in d.Frameworks) {
							if (fx.Profile == value && FrameworkInfoProvider.Instance.IsFrameworkProfileValid (fx.Framework, fx.Version, value)) {
								foundMatch = true;
							}
						}
						if (!foundMatch) {
							AddErrorWithArgs (CoreDiagnostics.UnknownTargetFrameworkProfile, value, d.Frameworks[0].Framework, d.Frameworks[0].Version);
						}
					}
					break;
				}
			}

			void AddErrorWithArgs (MSBuildDiagnosticDescriptor d, params object[] args) => Document.Diagnostics.Add (d, new TextSpan (offset, value.Length), args);

			// errors expected to be fixed by ChangeMisspelledNameFixProvider
			// captures the information needed by the fixer
			void AddFixableError (MSBuildDiagnosticDescriptor d, params object[] args)
			{
				Document.Diagnostics.Add (
					d,
					new TextSpan (offset, value.Length),
					ImmutableDictionary<string, object>.Empty
						.Add ("Name", value)
						.Add ("ValueKind", kind)
						.Add ("CustomType", info.CustomType),
					args
				);
			}
		}

		bool IsItemUsed (string itemName, ReferenceUsage usage)
		{
			// if it's been found in an imported file or an explicit schema, it counts as used
			var item = Document
				.GetSchemas (skipThisDocumentInferredSchema: true)
				.GetItem (itemName);
			if (item != null) {
				return true;
			}

			// if it's used in some other way in the current file, it's valid
			if (Document.InferredSchema.ItemUsage.TryGetValue (itemName, out var u)) {
				if ((u & usage) != 0) {
					return true;
				}
			}
			return false;
		}

		bool IsPropertyUsed (string propertyName, ReferenceUsage usage)
		{
			// if it's been found in an imported file or an explicit schema, it counts as used
			var metaInfo = Document
				.GetSchemas (skipThisDocumentInferredSchema: true)
				.GetProperty (propertyName, true);
			if (metaInfo != null) {
				return true;
			}

			// if it's used in some other way in the current file, it's valid
			if (Document.InferredSchema.PropertyUsage.TryGetValue (propertyName, out var u)) {
				if ((u & usage) != 0) {
					return true;
				}
			}
			return false;
		}

		bool IsMetadataUsed (string itemName, string metadataName, ReferenceUsage usage)
		{
			// if it's been found in an imported file or an explicit schema, it's valid
			var metaInfo = Document
				.GetSchemas (skipThisDocumentInferredSchema: true)
				.GetMetadata (itemName, metadataName, true);
			if (metaInfo != null) {
				return true;
			}

			// if it's used in some other way in the current file, it's valid
			if (Document.InferredSchema.MetadataUsage.TryGetValue ((itemName, metadataName), out var u)) {
				if ((u & usage) != 0) {
					return true;
				}
			}
			return false;
		}


		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Internal error in MSBuildDocumentValidator")]
		static partial void LogInternalError (ILogger logger, Exception ex);
	}
}