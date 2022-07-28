using System.Xml;
using System.Xml.Schema;
using MonoDevelop.MSBuild.Language.Typesystem;
using System.Diagnostics.CodeAnalysis;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Language;

namespace SchemaImporter;

class MSBuildXsdSchemaReader
{
	public const string MSBuildSchemaUri = "http://schemas.microsoft.com/developer/msbuild/2003";

	public MSBuildSchema Schema { get; }  = new();

	public void Read(XmlSchemaSet schemaSet)
	{
		foreach (System.Xml.Schema.XmlSchemaElement el in schemaSet.GlobalElements.Values) {
			if (el.SubstitutionGroup is System.Xml.XmlQualifiedName n && n.Namespace == MSBuildSchemaUri) {
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
		var docMarkup = GetDocMarkup(el);

		if (!CheckType(el, el.ElementSchemaType, out XmlSchemaComplexType? complexType)) {
			return null;
		}

		if ((complexType.BaseXmlSchemaType?.Name ?? complexType.Name) != "SimpleItemType") {
			LogUnhandled(complexType.BaseXmlSchemaType ?? complexType, "Item schema is not derived from SimpleItemType");
			return null;
		}

		string? includeDescription = null;
		var itemMetadata = ReadItemMetadataElements(complexType);

		// note: the includeDescription is not entirely correct, it's meant to be a noun, not a whole sentence
		return new ItemInfo(
			el.Name,
			docMarkup,
			includeDescription,
			metadata: itemMetadata);
	}

	Dictionary<string, MetadataInfo>? ReadItemMetadataElements(XmlSchemaComplexType complexType)
	{
		Dictionary<string, MetadataInfo>? itemMetadata = null;
		string? includeDescription = null;

		foreach (var metadataEl in EnumerateFromSequenceOrChoice<XmlSchemaElement>(complexType.ContentTypeParticle)) {
			if (metadataEl.Name is not string metadataName) {
				LogUnhandled(metadataEl, "Metadata definition has no name");
				continue;
			};

			var metadataDoc = GetDocMarkup(metadataEl);

			if (metadataName == "Include") {
				includeDescription = metadataDoc;
				continue;
			}

			var type = ConvertValueType(metadataEl);

			(itemMetadata ??= new Dictionary<string, MetadataInfo>()).Add(
				metadataName,
				new MetadataInfo(metadataName, metadataDoc, valueKind: type.kind, customType: type.customType)
			);
		}

		return itemMetadata;
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
				_ => null
			},
			_ => null
		};

	(MSBuildValueKind kind, CustomTypeInfo? customType) ConvertValueType(XmlSchemaElement el)
	{
		if (el.ElementSchemaType?.QualifiedName is XmlQualifiedName typeName) {
			if (MapBuiltinType(typeName) is MSBuildValueKind mappedKind) {
				return (mappedKind, null);
			}
			if (!typeName.IsEmpty) {
				LogUnhandled(el, $"Unknown named schema type '{typeName.Name}' on '{el.Name}'");
			}
		}

		MSBuildValueKind kind = MSBuildValueKind.Unknown;
		CustomTypeInfo? customType = null;
		if (el.ElementSchemaType is XmlSchemaSimpleType simpleType) {
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
						LogUnhandled(el, $"Unknown facet {facet.GetType()} on '{el.Name}'");
					}
				}
				customType = new CustomTypeInfo(enumValues);
				kind = MSBuildValueKind.CustomType;
			} else {
				LogUnhandled(el, $"Simple type has unknown content {simpleType.Content?.GetType()} on '{el.Name}'");
			}
		} else if (el.ElementSchemaType is XmlSchemaComplexType complexType) {
			// TODO
			if (!IsMetadataConditionAttribute(complexType)) {
				LogUnhandled(el, $"Unknown complex type on '{el.Name}'");
			}
		} else {
			LogUnhandled(el, $"Unknown schema type {el.ElementSchemaType?.GetType()} on '{el.Name}'");
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
		var docMarkup = GetDocMarkup(el);
		var type = ConvertValueType(el);

		//TODO: anything else we can salvage here?
		return new PropertyInfo(
			el.Name,
			docMarkup,
			valueKind: type.kind,
			customType: type.customType);
	}

	TaskInfo? ReadTask(XmlSchemaElement el)
	{
		var docMarkup = GetDocMarkup(el);

		//TODO: read parameters

		return new TaskInfo(el.Name, docMarkup, false);
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
		Console.WriteLine(message);
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
			if (node is System.Xml.XmlComment) {
				continue;
			}
			if (node is not System.Xml.XmlText textNode) {
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
