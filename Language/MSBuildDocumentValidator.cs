// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.MSBuildEditor.ExpressionParser;
using System.Collections.Generic;
using System;
using System.Globalization;

namespace MonoDevelop.MSBuildEditor.Language
{
	class MSBuildDocumentValidator : MSBuildResolvingVisitor
	{
		readonly string filename;

		public MSBuildDocumentValidator (MSBuildResolveContext context, string filename) : base (context)
		{
			this.filename = filename;
		}

		void AddError (ErrorType errorType, string message, DocumentRegion region) => Context.Errors.Add (new Error (errorType, message, region));
		DocumentRegion GetRegion (int offset, int length) => new DocumentRegion (Document.OffsetToLocation (offset), Document.OffsetToLocation (offset + length));
		void AddError (string message, DocumentRegion region) => AddError (ErrorType.Error, message, region);
		void AddError (string message, int offset, int length) => AddError (ErrorType.Error, message, GetRegion (offset, length));
		void AddWarning (string message, DocumentRegion region) => AddError (ErrorType.Warning, message, region);
		void AddWarning (string message, int offset, int length) => AddError (ErrorType.Warning, message, GetRegion (offset, length));


		protected override void VisitUnknownElement (XElement element)
		{
			AddError ($"Unknown element '{element.Name.FullName}'", element.Region);
			base.VisitUnknownElement (element);
		}

		protected override void VisitUnknownAttribute (XElement element, XAttribute attribute)
		{
			AddError ($"Unknown attribute '{attribute.Name.FullName}'", attribute.Region);
			base.VisitUnknownAttribute (element, attribute);
		}

		protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			foreach (var rat in resolved.Attributes) {
				if (rat.Required && !rat.IsAbstract) {
					var xat = element.Attributes.Get (new XName (rat.Name), true);
					if (xat == null) {
						AddError ($"{element.Name.Name} must have attribute {rat.Name}", element.GetNameRegion ());
					}
				}
			}

			switch (resolved.Kind) {
			case MSBuildKind.Project:
				if (!filename.EndsWith (".props", System.StringComparison.OrdinalIgnoreCase)) {
					ValidateProjectHasTarget (element);
				}
				break;
			case MSBuildKind.OnError:
				ValidateOnErrorOnlyFollowedByOnError (element);
				break;
			case MSBuildKind.Otherwise:
				ValidateOtherwiseIsLastElement (element);
				break;
			case MSBuildKind.Output:
				ValidateOutputHasPropertyOrItemName (element);
				break;
			case MSBuildKind.UsingTask:
				ValidateUsingTaskHasAssembly (element);
				break;
			case MSBuildKind.Import:
				ValidateImportOnlyHasVersionIfHasSdk (element);
				break;
			case MSBuildKind.Item:
				ValidateItemAttributes (element);
				break;
			}

			base.VisitResolvedElement (element, resolved);
		}

		void ValidateProjectHasTarget (XElement element)
		{
			if (element.Attributes.Get (new XName ("Sdk"), true) != null) {
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

			AddWarning ($"Project should have Sdk, Target or Import", element.GetNameRegion ());
		}

		void ValidateOnErrorOnlyFollowedByOnError (XElement element)
		{
			if (!element.NextSiblingElement ().NameEquals ("OnError", true)) {
				AddError (
					$"OnError may only be followed by other OnError elements",
					element.NextSiblingElement ().GetNameRegion ());
			}
		}

		void ValidateOtherwiseIsLastElement (XElement element)
		{
			if (element.NextSiblingElement () != null) {
				AddError (
					$"Otherwise must be the last element in a Choose",
					element.NextSiblingElement ().GetNameRegion ());
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
				AddError (
					$"Output element must have PropertyName or ItemName attribute",
					element.GetNameRegion ());
			}
		}

		void ValidateUsingTaskHasAssembly (XElement element)
		{
			bool foundAssemblyAttribute = false;
			foreach (var att in element.Attributes) {
				if (att.NameEquals ("AssemblyName", true) || att.NameEquals ("AssemblyFile", true)) {
					if (foundAssemblyAttribute) {
						AddError (
							$"UsingTask may have only one AssemblyName or AssemblyFile attribute",
							att.GetNameRegion ());
					}
					foundAssemblyAttribute = true;
				}
			}
			if (!foundAssemblyAttribute) {
				AddError (
					$"UsingTask must have AssemblyName or AssemblyFile attribute",
					element.GetNameRegion ());
			}

			bool foundParameterGroup = false, foundTaskBody = false;
			foreach (var child in element.Elements) {
				if (child.NameEquals ("ParameterGroup", true)) {
					if (foundParameterGroup) {
						AddError (
							$"UsingTask may only have one ParameterGroup",
							child.GetNameRegion ());
					}
					foundParameterGroup = true;
				}
				if (child.NameEquals ("TaskBody", true)) {
					if (foundTaskBody) {
						AddError (
							$"UsingTask may only have one TaskBody",
							child.GetNameRegion ());
					}
					foundTaskBody = true;
				}
			}
			if (foundParameterGroup != foundTaskBody) {
				AddError (
					$"UsingTask must have both TaskBody and ParameterGroup, or neither",
					element.GetNameRegion ());
			}
		}

		void ValidateImportOnlyHasVersionIfHasSdk (XElement element)
		{
			if (element.Attributes.Get (new XName ("Sdk"), true) != null) {
				return;
			}

			foreach (var att in element.Attributes) {
				if (att.NameEquals ("Version", true)) {
					AddError (
						$"Import may only have a Version if it has an Sdk",
						att.GetNameRegion ());
				}
				if (att.NameEquals ("MinVersion", true)) {
					AddError (
						$"Import may only have a MinVersion if it has an Sdk",
						att.GetNameRegion ());
				}
			}
		}

		void ValidateItemAttributes (XElement element)
		{
			bool hasInclude = false, hasUpdate = false, hasRemove = false;
			foreach (var att in element.Attributes) {
				hasInclude |= att.NameEquals ("Include", true);
				hasUpdate |= att.NameEquals ("Update", true);
				hasRemove |= att.NameEquals ("Remove", true);
				if (att.NameEquals ("KeepMetadata", true) || att.NameEquals ("RemoveMetadata", true) || att.NameEquals ("KeepDuplicates", true)) {
					if (!(element.Parent?.Parent is XElement t && t.NameEquals ("Target", true))) {
						AddError (
							$"{att.Name.Name} is only valid within a target",
							att.GetNameRegion ());
					}
				}
			}

			if (!hasInclude && !hasRemove && !hasUpdate) {
				AddError (
					$"Items must have Include, Update or Remove attributes",
					element.GetNameRegion ());
			}
		}

		protected override void VisitResolvedAttribute (XElement element, XAttribute attribute, MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
		{
			ValidateAttribute (element, attribute, resolvedElement, resolvedAttribute);
			base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);
		}

		void ValidateAttribute (XElement element, XAttribute attribute, MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
		{
			if (string.IsNullOrWhiteSpace (attribute.Value)) {
				if (resolvedAttribute.Required) {
					AddError ($"Required attribute has empty value", attribute.GetNameRegion ());
				} else {
					AddWarning ($"Attribute has empty value", attribute.GetNameRegion ());
				}
				return;
			}
		}

		protected override void VisitValue (ValueInfo info, string value, int offset)
		{
			if (info.DefaultValue != null && string.Equals (info.DefaultValue, value)) {
				AddWarning ($"{info.GetTitleCaseKindName ()} has default value", offset, value.Length);
			}

			base.VisitValue (info, value, offset);
		}

		protected override void VisitValueExpression (ValueInfo info, MSBuildValueKind kind, Expression expression, int offset, int length)
		{
			base.VisitValueExpression (info, kind, expression, offset, length);

			bool allowExpressions = kind.AllowExpressions ();
			bool allowLists = kind.AllowLists () || info.ValueSeparators?.Length > 0;

			for (int i = 0; i < expression.Collection.Count; i++) {
				var val = expression.Collection [i];
				if (val is InvalidExpressionError err) {
					var errOffset = offset+ err.Position;
					AddError (
						$"Invalid expression: {err.Message}",
						new DocumentRegion (
							Document.OffsetToLocation (errOffset),
							Document.OffsetToLocation (errOffset + (length - err.Position))
						)
					);
					return;
				}
				if (val is string s) {
					if (s == ";") {
						if (!allowLists) {
							AddValueError ($"{Name()} does not allow lists");
							return;
						}
						continue;
					}
				}
				if (!allowExpressions) {
					AddValueError ($"{Name()} does not allow expressions");
				}

				//items are implicitly lists
				if (val is ItemReference ir) {
					if (!allowLists) {
						AddValueError ($"{Name ()} does not allow lists");
						return;
					}
				}

				//TODO: can we validate property/metadata/items refs?
			}

			void AddValueError (string e) => AddError (e, offset, length);
			string Name () => DescriptionFormatter.GetTitleCaseKindName (info);
		}

		protected override void VisitExpressionLiteral (ValueInfo info, MSBuildValueKind kind, string value, int offset, int length)
		{
			IReadOnlyList<ConstantInfo> knownVals = info.Values ?? kind.GetSimpleValues (false);

			if (knownVals != null && knownVals.Count != 0) {
				foreach (var kv in knownVals) {
					if (string.Equals (kv.Name, value, StringComparison.OrdinalIgnoreCase)) {
						return;
					}
				}
				AddError ($"Unknown value '{value}'");
				return;
			}
			switch (kind) {
			case MSBuildValueKind.Guid:
				if (!Guid.TryParseExact (value, "B", out _)) {
					AddError ("Invalid GUID value");
				}
				break;
			case MSBuildValueKind.Int:
				if (!long.TryParse (value, out _)) {
					AddError ("Invalid integer value");
				}
				break;
			case MSBuildValueKind.Bool:
				if (!bool.TryParse (value, out _)) {
					AddError ("Invalid boolean value");
				}
				break;
			case MSBuildValueKind.Url:
				if (!Uri.TryCreate (value, UriKind.Absolute, out _)) {
					AddError ("Invalid URL");
				}
				break;
			case MSBuildValueKind.Version:
				if (!Version.TryParse (value, out _)) {
					AddError ("Invalid version");
				}
				break;
			case MSBuildValueKind.TargetName:
				if (Context.GetSchemas ().GetTarget (value) == null) {
					AddWarning ("Target is not defined");
				}
				break;
			case MSBuildValueKind.PropertyName:
				if (Context.GetSchemas ().GetProperty (value) == null) {
					AddWarning ("Unknown property name");
				}
				break;
			case MSBuildValueKind.ItemName:
				if (Context.GetSchemas ().GetItem (value) == null) {
					AddWarning ("Unknown item name");
				}
				break;
			case MSBuildValueKind.Lcid:
				if (int.TryParse (value, out int lcid) && lcid > 0) {
					try {
						CultureInfo.GetCultureInfo (lcid);
					} catch (CultureNotFoundException) {
						AddError ("Unknown LCID");
					}
				} else {
					AddError ("Invalid LCID");
				}
				break;
			}

			void AddError (string e) => this.AddError (e, offset, value.Length);
			void AddWarning (string e) => this.AddWarning (e, offset, value.Length);
		}
	}
}