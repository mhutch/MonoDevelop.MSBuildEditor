// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml;
using System.Xml.Schema;
using System.Diagnostics.CodeAnalysis;

using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Language;

namespace SchemaImporter;

class MSBuildXsdSchemaReader
{
	public const string MSBuildSchemaUri = "http://schemas.microsoft.com/developer/msbuild/2003";

	public MSBuildSchema Schema { get; } = [];

	public void Read(XmlSchemaSet schemaSet)
	{
		foreach (XmlSchemaElement el in schemaSet.GlobalElements.Values) {
			if (el.SubstitutionGroup is XmlQualifiedName n && n.Namespace == MSBuildSchemaUri) {
				if (string.IsNullOrEmpty (el.Name)) {
					LogUnhandled(el, "Global element has no name");
					continue;
				}
				ISymbol? symbol = n.Name switch {
					"Item" => ReadItem(el),
					"Property" => ReadProperty(el),
					"Task" => ReadTask(el),
					_ => null
				};
				if (symbol is not null) {
					Schema.Add(symbol);
					continue;
				}
				LogUnhandled(el, $"Cannot handle global element '{el.Name}' with substitutionGroup '{el.SubstitutionGroup}'");
			}
		}
	}

	ItemInfo? ReadItem(XmlSchemaElement el)
	{
		string name = el.Name!; // caller checks this
		var docMarkup = GetDocMarkup(el);

		if (!CheckType(el, el.ElementSchemaType, out XmlSchemaComplexType? complexType)) {
			return null;
		}

		if ((complexType.BaseXmlSchemaType?.Name ?? complexType.Name) != "SimpleItemType") {
			LogUnhandled(complexType.BaseXmlSchemaType ?? complexType, "Item schema is not derived from SimpleItemType");
			return null;
		}

		var itemMetadata = ReadItemMetadataElements(complexType, out string? includeDescription);

		// note: the includeDescription is not entirely correct, it's meant to be a noun, not a whole sentence
		return new ItemInfo(
			name,
			docMarkup,
			includeDescription,
			metadata: itemMetadata);
	}

	Dictionary<string, MetadataInfo>? ReadItemMetadataElements(XmlSchemaComplexType complexType, out string? includeDescription)
	{
		Dictionary<string, MetadataInfo>? itemMetadata = null;
		includeDescription = null;

		foreach (var metadataEl in EnumerateFromSequenceOrChoice<XmlSchemaElement>(complexType.ContentTypeParticle)) {
			if (metadataEl.Name is not string metadataName) {
				LogUnhandled(metadataEl, "Metadata definition has no name");
				continue;
			};

			var metadataDoc = GetDocMarkup(metadataEl);

			if (metadataName == "Include" && !string.IsNullOrEmpty(metadataDoc)) {
				includeDescription = metadataDoc;
				continue;
			}

			var type = ConvertValueType(metadataEl, metadataName, metadataEl.ElementSchemaType);

			AddIfBetter(ref itemMetadata, new MetadataInfo(metadataName, metadataDoc, valueKind: type.kind, customType: type.customType, required: false));
		}

		if (complexType.ContentModel?.Content is XmlSchemaComplexContentExtension extension) {
			foreach (var att in extension.Attributes) {
				if (att is not XmlSchemaAttribute metadataAtt) {
					continue;
				}
				if (metadataAtt.Name is not string metadataName) {
					LogUnhandled(metadataAtt, "Metadata definition has no name");
					continue;
				}

				bool required = metadataAtt.Use == XmlSchemaUse.Required;

				var metadataDoc = GetDocMarkup(metadataAtt);

				if (metadataName == "Include" && !string.IsNullOrEmpty(metadataDoc)) {
					includeDescription = metadataDoc;
					continue;
				}

				var type = ConvertValueType(metadataAtt, metadataName, metadataAtt.AttributeSchemaType);

				AddIfBetter(ref itemMetadata, new MetadataInfo(metadataName, metadataDoc, valueKind: type.kind, customType: type.customType, required: required));
			}
		}

		return itemMetadata;
	}

	// FIXME: inspecting the ContentTypeParticle may duplicate items we got from the ContentModel.Content, can we simplify to a single loop?
	// there also may be duplicate items in the XSD itself, check which has the most information
	static void AddIfBetter(ref Dictionary<string, MetadataInfo>? itemMetadata, MetadataInfo meta)
	{
		itemMetadata ??= new();

		if (!itemMetadata.TryGetValue(meta.Name, out MetadataInfo? existing)) {
			itemMetadata[meta.Name] = meta;
			return;
		}

		if (existing.Description.Text == meta.Description.Text && existing.ValueKind == meta.ValueKind && existing.Required == meta.Required) {
			return;
		}

		// if they are not identical, merge info from both
		itemMetadata[meta.Name] = new MetadataInfo(
			existing.Name,
			string.IsNullOrEmpty(existing.Description.Text)? meta.Description.Text : existing.Description.Text,
			valueKind: existing.ValueKind == MSBuildValueKind.Unknown? meta.ValueKind : existing.ValueKind,
			customType: existing.CustomType ?? meta.CustomType,
			required: existing.Required || meta.Required
		);
	}

	/// <summary>
	/// We expect sequence/choice/element because that's the pattern most definitions have copypasted.
	/// But if the choice has a single element, the schema reader will elide it as it's redundant.
	/// And some definitions omit the sequence as it's also redundant, or use other variants like `all`
	/// </summary>
	IEnumerable<T> EnumerateFromSequenceOrChoice<T>(XmlSchemaObject unknown) where T : XmlSchemaObject
	{
		if (unknown is T t) {
			return EnumerateSingle(t);
		}

		if (unknown is XmlSchemaSequence seq) {
			// if it's a single item, unwrap it
			if (seq.Items.Count == 1) {
				return EnumerateFromSequenceOrChoice<T>(seq.Items[0]);
			}
			// if first item is target type, assume it's a simple sequence and validate
			if (seq.Items.Count > 1 && seq.Items[0] is T) {
				return CheckCollection<T>(seq, seq.Items);
			}
			// otherwise, we can't handle it
			LogUnhandled(unknown, "Empty or mixed sequence");
			return Enumerable.Empty<T>();
		}

		if (unknown is XmlSchemaChoice choice) {
			return CheckCollection<T>(choice, choice.Items);
		}

		// todo: does this imply they're required?
		if (unknown is XmlSchemaAll all) {
			return CheckCollection<T>(all, all.Items);
		}

		// is there a better way to handle XmlSchemaParticle.EmptyParticle?
		if (unknown is XmlSchemaParticle particle && particle.Parent is null) {
			return Enumerable.Empty<T>();
		}

		LogUnhandled(unknown, $"Unhandled node '{unknown.GetType()}'");
		return Enumerable.Empty<T>();
	}

	static IEnumerable<T> EnumerateSingle<T>(T value)
	{
		yield return value;
	}

	MSBuildValueKind? MapBuiltinType(XmlQualifiedName typeName)
		=> typeName.Namespace switch {
			MSBuildSchemaUri => typeName.Name switch {
				"boolean" => MSBuildValueKind.Bool,
				"StringPropertyType" => MSBuildValueKind.String,
				"GenericPropertyType" => MSBuildValueKind.Unknown,
				"GenericItemType" => MSBuildValueKind.UnknownItem,
				_ => null
			},
			XmlSchema.Namespace => typeName.Name switch {
				"boolean" => MSBuildValueKind.Bool,
				"anyType" => MSBuildValueKind.Unknown,
				"anySimpleType" => MSBuildValueKind.Unknown,
				"string" => MSBuildValueKind.String,
				_ => null
			},
			_ => null
		};

	(MSBuildValueKind kind, CustomTypeInfo? customType) ConvertValueType (XmlSchemaObject el, string name, XmlSchemaType? type)
	{
		if (type is null) {
			return (MSBuildValueKind.Unknown, null);
		}

		if (type.QualifiedName is XmlQualifiedName typeName) {
			if (MapBuiltinType(typeName) is MSBuildValueKind mappedKind) {
				return (mappedKind, null);
			}
			if (!typeName.IsEmpty) {
				LogUnhandled(el, $"Unknown named schema type '{typeName.Name}' on '{name}'");
			}
		}

		MSBuildValueKind kind = MSBuildValueKind.Unknown;
		CustomTypeInfo? customType = null;
		if (type is XmlSchemaSimpleType simpleType) {
			if (simpleType.Content is XmlSchemaSimpleTypeRestriction restriction) {
				// TODO: use this
				var baseType = simpleType.BaseXmlSchemaType?.QualifiedName;
				var enumValues = new List<CustomTypeValue>();
				foreach (var facet in restriction.Facets) {
					if (facet is XmlSchemaEnumerationFacet enumFacet) {
						if(enumFacet.Value is string enumValue) {
							var enumValueDocs = GetDocMarkup(enumFacet);
							enumValues.Add(new CustomTypeValue (enumValue, enumValueDocs));
						}
					} else {
						LogUnhandled(el, $"Unknown facet {facet.GetType()} on '{name}'");
					}
				}
				customType = new CustomTypeInfo(enumValues);
				kind = MSBuildValueKind.CustomType;
			} else {
				LogUnhandled(el, $"Simple type has unknown content {simpleType.Content?.GetType()} on '{name}'");
			}
		} else if (type is XmlSchemaComplexType complexType) {
			// TODO
			if (!IsMetadataConditionAttribute(complexType)) {
				LogUnhandled(el, $"Unknown complex type on '{name}'");
			}
		} else {
			LogUnhandled(el, $"Unknown schema type {type?.GetType()} on '{name}'");
		}

		return (kind, customType);
	}

	// Pattern used by ClCompile.PrecompiledHeader etc to add "Condition" attribute to metadata.
	// We can ignore this because the MSBuild language service supports Condition attributes
	// automatically on all valid elements
	static bool IsMetadataConditionAttribute(XmlSchemaComplexType complexType) =>
		complexType.DerivedBy == XmlSchemaDerivationMethod.Extension
		&& complexType.BaseXmlSchemaType?.QualifiedName is XmlQualifiedName qn
		&& qn.Namespace == XmlSchema.Namespace && qn.Name == "string"
		&& complexType.ContentModel is XmlSchemaSimpleContent simpleContent
		&& simpleContent.Content is XmlSchemaSimpleContentExtension simpleContentExtension
		&& simpleContentExtension.Attributes.Count == 1
		&& simpleContentExtension.Attributes[0] is XmlSchemaAttribute att
		&& att.Name == "Condition";

	PropertyInfo? ReadProperty(XmlSchemaElement el)
	{
		string name = el.Name!; // caller checks this

		var docMarkup = GetDocMarkup(el);
		var type = ConvertValueType(el, name, el.ElementSchemaType);

		//TODO: anything else we can salvage here?
		return new PropertyInfo(
			name,
			docMarkup,
			valueKind: type.kind,
			customType: type.customType);
	}

	TaskInfo? ReadTask(XmlSchemaElement el)
	{
		var name = el.Name!; // only gets called when el.Name is not null
		var docMarkup = GetDocMarkup(el);

		//TODO: read parameters

		return new TaskInfo(name, docMarkup, TaskDeclarationKind.Inferred, null, null, null, null, 0, null);
	}

	IEnumerable<T> CheckCollection<T>(XmlSchemaObject parent, XmlSchemaObjectCollection collection) where T : XmlSchemaObject
	{
		foreach (var item in collection) {
			if (CheckType(parent, item, out T? val)) {
				yield return val;
			}
		}
	}

	bool CheckType<T>(XmlSchemaObject parent, XmlSchemaObject? value, [NotNullWhen(true)] out T? result) where T : XmlSchemaObject
	{
		result = value as T;
		if (result != null) {
			return true;
		}
		LogUnhandled(value ?? parent, $"Expected {typeof(T)}, got {value?.GetType()?.ToString() ?? "null"}");
		return false;
	}

	void LogUnhandled(XmlSchemaObject? location, string message)
	{
		if (location is not null && location.SourceUri is not null) {
			var file = Path.GetFileName(location.SourceUri);
			Console.Error.WriteLine ($"{file}({location.LineNumber}): {message}");
		} else {
			Console.Error.WriteLine (message);
		}
	}

	string? GetDocMarkup(XmlSchemaAnnotated annotated)
	{
		if (annotated.Annotation is not XmlSchemaAnnotation annotation) {
			return null;
		}
		XmlSchemaDocumentation? doc = null;
		foreach (var item in annotation.Items) {
			if (doc != null || item is not XmlSchemaDocumentation d) {
				LogUnhandled(item, $"Unknown annotation {item}");
				continue;
			}
			doc = d;
		}
		return GetMarkupFromDocNode(doc);
	}

	string? GetMarkupFromDocNode(XmlSchemaDocumentation? doc)
	{
		if (doc?.Markup is null) {
			return null;
		}
		string? text = null;
		foreach (var node in doc.Markup) {
			if (node is XmlComment) {
				continue;
			}
			if (node is not XmlText textNode) {
				LogUnhandled(doc, $"Unknown node in doc markup {node}");
				continue;
			}
			if (text != null) {
				LogUnhandled(doc, "Multiple text nodes in doc markup");
				continue;
			}
			text = textNode.Value;
		}
		return text?.Trim();
	}
}
