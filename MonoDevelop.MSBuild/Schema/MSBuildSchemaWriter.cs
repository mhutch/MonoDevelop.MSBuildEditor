// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Typesystem;

using Newtonsoft.Json;

namespace MonoDevelop.MSBuild.Schema;

/// <summary>
/// Writes a schema as a JSON file. Currently omits some information, and does not roundtrip JSON references and $appliesTo metadata as they are fully resolved during schema load.
/// </summary>
class MSBuildSchemaWriter : IDisposable
{
	readonly JsonTextWriter json;
	readonly Dictionary<MSBuildValueKind, string> kindNameMap;

	public MSBuildSchemaWriter (TextWriter writer)
	{
		json = new JsonTextWriter (writer) {
			Formatting = Formatting.Indented
		};
		kindNameMap = MSBuildSchema.GetValueKindNames ().ToDictionary (v => v.kind, v => v.name);
	}

	public void Dispose () => ((IDisposable)json)?.Dispose ();

	static IEnumerable<TSymbol> SortedByName<TSymbol> (Dictionary<string, TSymbol> dictionary) where TSymbol : BaseSymbol => dictionary.Values.OrderBy (p => p.Name, StringComparer.OrdinalIgnoreCase);

	public void Write (MSBuildSchema schema)
	{
		json.WriteStartObject ();

		json.WritePropertyName ("properties");
		json.WriteStartObject ();
		foreach (var property in SortedByName (schema.Properties)) {
			json.WritePropertyName (property.Name);
			WriteProperty (property);
		}
		json.WriteEndObject ();

		json.WritePropertyName ("items");
		json.WriteStartObject ();
		WriteItems (SortedByName (schema.Items));
		json.WriteEndObject ();

		json.WritePropertyName ("tasks");
		json.WriteStartObject ();
		foreach (var task in SortedByName (schema.Tasks)) {
			json.WritePropertyName (task.Name);
			WriteTask (task);
		}
		json.WriteEndObject ();

		json.WritePropertyName ("targets");
		json.WriteStartObject ();
		foreach (var target in SortedByName (schema.Targets)) {
			json.WritePropertyName (target.Name);
			WriteTarget (target);
		}
		json.WriteEndObject ();

		json.WriteEndObject ();
	}

	void WriteItems (IEnumerable<ItemInfo> items)
	{
		foreach (var item in items) {
			json.WritePropertyName (item.Name);
			WriteItem (item);
		}
	}

	void WriteItem (ItemInfo item)
	{
		if (IsOnlyDescription (item)) {
			WriteDescription (item);
			return;
		}

		json.WriteStartObject ();

		WriteDescriptionWithKey (item);
		WriteIncludeDescriptionWithKey (item);
		WriteTypeWithKey (item);

		if (item.Metadata.Count > 0) {
			json.WritePropertyName ("metadata");
			var sortedMetadata = SortedByName (item.Metadata);
			/*
			if (item.Metadata.Values.All (m => m.Description.IsEmpty && IsOnlyDescription (m))) {
				WriteMetadataAsArray (sortedMetadata);
			} else {*/
			WriteMetadataAsObject (sortedMetadata);
		}

		json.WriteEndObject ();
	}

	// writing metadata as an array is not actually valid right now
	/*
	void WriteMetadataAsArray (IEnumerable<MetadataInfo> metadata)
	{
		json.WriteStartArray ();
		foreach (var m in metadata) {
			json.WriteValue (m.Name);
		}
		json.WriteEndArray ();
	}*/

	void WriteMetadataAsObject (IEnumerable<MetadataInfo> metadata)
	{
		json.WriteStartObject ();
		foreach (var m in metadata) {
			json.WritePropertyName (m.Name);
			WriteMetadata (m);
		}
		json.WriteEndObject ();
	}

	void WriteMetadata (MetadataInfo metadata)
	{
		if (IsOnlyDescription (metadata)) {
			WriteDescription (metadata);
			return;
		}

		json.WriteStartObject ();

		WriteDescriptionWithKey (metadata);
		WriteTypeWithKey (metadata);

		if (metadata.Required) {
			json.WritePropertyName ("required");
			json.WriteValue (true);
		}

		json.WriteEndObject ();
	}

	void WriteProperty (PropertyInfo property)
	{
		if (IsOnlyDescription (property)) {
			WriteDescription (property);
			return;
		}

		json.WriteStartObject ();

		WriteDescriptionWithKey (property);
		WriteTypeWithKey (property);

		json.WriteEndObject ();
	}

	void WriteTask (TaskInfo task)
	{
		if (task.Parameters.Count == 0) {
			WriteDescription (task);
			return;
		}

		json.WriteStartObject ();

		WriteDescriptionWithKey (task);

		// TODO: write parameters
		// task.Parameters

		json.WriteEndObject ();
	}

	void WriteTarget (TargetInfo target)
	{
		WriteDescription (target);
	}

	void WriteDescription (ISymbol symbol) => json.WriteValue (symbol.Description.Text ?? "");

	void WriteDescriptionWithKey (ISymbol symbol)
	{
		if (!symbol.Description.IsEmpty) {
			json.WritePropertyName ("description");
			WriteDescription (symbol);
		}
	}

	void WriteIncludeDescriptionWithKey (ItemInfo symbol)
	{
		if (!string.IsNullOrEmpty (symbol.IncludeDescription)) {
			json.WritePropertyName ("includeDescription");
			json.WriteValue (symbol.IncludeDescription ?? "");
		}
	}

	void WriteTypeWithKey (ITypedSymbol typedSymbol)
	{
		if (typedSymbol.ValueKind == MSBuildValueKind.Unknown) {
			return;
		}

		if (typedSymbol.ValueKind == MSBuildValueKind.CustomType) {
			json.WritePropertyName ("type");
			WriteCustomType (typedSymbol.CustomType);
			return;
		}

		if (kindNameMap.TryGetValue (typedSymbol.ValueKind, out var name)) {
			json.WritePropertyName ("type");
			json.WriteValue (name);
		}
	}

	void WriteCustomType (CustomTypeInfo customType)
	{
		if (customType.Values.All (v => v.Description.IsEmpty)) {
			json.WriteStartArray ();
			foreach (var value in customType.Values) {
				json.WriteValue (value.Name);
			}
			json.WriteEndArray ();
		} else {
			json.WriteStartObject ();
			foreach (var value in customType.Values) {
				json.WritePropertyName (value.Name);
				WriteDescription (value);
			}
			json.WriteEndObject ();
		}
	}

	static bool IsOnlyDescription (PropertyInfo property) =>
		IsOnlyDescription ((VariableInfo)property)
		&& property.Reserved == false;

	static bool IsOnlyDescription (ItemInfo item) =>
		IsOnlyDescription ((VariableInfo)item)
		&& string.IsNullOrEmpty (item.IncludeDescription)
		&& item.Metadata.Count == 0;

	static bool IsOnlyDescription (MetadataInfo metadata) =>
		IsOnlyDescription ((VariableInfo)metadata)
		&& metadata.Required == false
		&& metadata.Reserved == false;

	static bool IsOnlyDescription (VariableInfo variable) =>
		variable.DefaultValue is null
		&& variable.ValueKind == MSBuildValueKind.Unknown
		&& variable.CustomType is null
		&& variable.IsDeprecated == false;

	static bool IsOnlyDescription (TaskInfo task) =>
		task.Parameters.Count == 0;
}
