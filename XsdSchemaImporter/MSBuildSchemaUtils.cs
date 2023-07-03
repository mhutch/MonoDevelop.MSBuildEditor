// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;

using Newtonsoft.Json;

static class MSBuildSchemaUtils
{

	/// <summary>
	/// Returns a schema containing symbols from <c>other</c> that differ from those in the <c>basis</c>
	/// </summary>
	public static MSBuildSchema GetAddedOrChanged(MSBuildSchema basisSchema, MSBuildSchema otherSchema)
	{
		// ignore description docs in otherSchema when the basisSchema already has a description
		const bool ignoreDifferentDocs = true;

		// ignore valueKinds in otherSchema when the basisSchema already has a non-unknown valueKind
		const bool ignoreDifferentValueKinds = true;

		// ignore string valueKinds in otherSchema as the the XSD schema often treats strings as default
		const bool treatStringValueKindAsUnknownInOther = true;

		MSBuildValueKind MapStringKindToUnknown(MSBuildValueKind kind) => treatStringValueKindAsUnknownInOther && kind == MSBuildValueKind.String? MSBuildValueKind.Unknown : kind;

		bool IsBetterDescription(BaseSymbol basis, BaseSymbol other) => IsBetterDocString(basis.Description.Text, other.Description.Text);
		bool IsBetterDocString(string basis, string other) => !string.IsNullOrEmpty(other) && (string.IsNullOrEmpty(basis) || (!ignoreDifferentDocs && !string.Equals(basis, other)));
		bool IsBetterValueKind(VariableInfo basis, VariableInfo other)
		{
			if (basis.ValueKind == MSBuildValueKind.Unknown) {
				return MapStringKindToUnknown (other.ValueKind) != MSBuildValueKind.Unknown;
			}
			return ignoreDifferentValueKinds? false : other.ValueKind != basis.ValueKind;
		}

		var diff = new MSBuildSchema();

		foreach (var otherItem in otherSchema.Items.Values) {
			if (!basisSchema.Items.TryGetValue(otherItem.Name, out var basisItem)) {
				diff.Items.Add(otherItem.Name, otherItem);
				continue;
			}

			//TODO: check the item's customtype, deprecation messdage, deprecation state
			bool betterDescription = IsBetterDescription(basisItem, otherItem);
			bool betterValueKind = IsBetterValueKind(basisItem, otherItem);
			bool betterIncludeDescription = IsBetterDocString(basisItem.IncludeDescription, otherItem.IncludeDescription);
			bool itemIsBetter = betterDescription || betterValueKind || betterIncludeDescription;

			var newMetadata = new Dictionary<string,MetadataInfo>();
			foreach ((var metadataName, var otherMetadata) in otherItem.Metadata) {
				// TODO: check more properties of the metadata
				if (basisItem.Metadata.TryGetValue(metadataName, out var basisMetadata)) {
					if (IsBetterDescription(basisMetadata, otherMetadata) || IsBetterValueKind(basisMetadata, otherMetadata)) {
						newMetadata.Add(metadataName, otherMetadata);
					}
				}
			}

			if (itemIsBetter || newMetadata.Count > 0) {
				diff.Items.Add(otherItem.Name, new ItemInfo(
					otherItem.Name,
					betterDescription? otherItem.Description.Text : null,
					betterIncludeDescription? otherItem.IncludeDescription : null,
					betterValueKind? otherItem.ValueKind : MSBuildValueKind.Unknown,
					betterValueKind? otherItem.CustomType : null,
					newMetadata.Count > 0? newMetadata : null,
					false,
					null
				));
			}
		}

		foreach (var op in otherSchema.Properties.Values) {
			var otherProp = op;
			if (treatStringValueKindAsUnknownInOther && otherProp.ValueKind == MSBuildValueKind.String) {
				otherProp = new PropertyInfo(otherProp.Name, otherProp.Description, otherProp.Reserved, MSBuildValueKind.Unknown, null, otherProp.DefaultValue, otherProp.IsDeprecated, otherProp.DeprecationMessage);
			}
			if (!basisSchema.Properties.TryGetValue(otherProp.Name, out var basisProp)) {
				if (!otherProp.Description.IsEmpty || otherProp.ValueKind != MSBuildValueKind.Unknown) {
					diff.Properties.Add(otherProp.Name, otherProp);
				}
				continue;
			}

			if (IsBetterDescription(basisProp, otherProp) || IsBetterValueKind (basisProp, otherProp)) {
				diff.Properties.Add(otherProp.Name, otherProp);
			}
		}

		foreach (var otherTask in otherSchema.Tasks.Values) {
			// ignore tasks without descriptions & parameters, we'll pick them up from inference
			if (otherTask.Description.IsEmpty && otherTask.Parameters.Count == 0) {
				continue;
			}
			//TODO: diff task parameters
			if (!basisSchema.Tasks.TryGetValue(otherTask.Name, out var basisTask) || IsBetterDescription(basisTask, otherTask)) {
				diff.Tasks.Add(otherTask.Name, otherTask);
			};
		}

		foreach (var otherTarget in otherSchema.Targets.Values) {
			if (!basisSchema.Targets.TryGetValue(otherTarget.Name, out var basisTarget) || IsBetterDescription(otherTarget, basisTarget)) {
				diff.Targets.Add(otherTarget.Name, otherTarget);
			}
		}

		return diff;
	}

	public static MSBuildSchema CombineSchemas(IEnumerable<MSBuildSchema> schemas)
	{
		var combined = new MSBuildSchema();
		foreach (var s in schemas) {
			AddRangeOfItems(combined.Items, s.Items);
			AddRange(combined.Properties, s.Properties);
			AddRange(combined.Tasks, s.Tasks);
			AddRange(combined.Targets, s.Targets);
		}
		return combined;
	}

	static void AddRange<K, V>( Dictionary<K, V> d, IEnumerable<KeyValuePair<K, V>> range) where K : notnull
	{
		foreach (var kv in range) {
			d.Add(kv.Key, kv.Value);
		}
	}

	static void AddRangeOfItems(Dictionary<string, ItemInfo> d, IEnumerable<KeyValuePair<string, ItemInfo>> range)
	{
		foreach (var item in range) {
			if (d.TryGetValue(item.Key, out var mergeTo)) {
				var mergeFrom = item.Value;
				if (mergeTo.Description.IsEmpty && !mergeFrom.Description.IsEmpty) {
					(mergeTo, mergeFrom) = (mergeFrom, mergeTo);
					d[item.Key] = item.Value;
				}
				foreach (var meta in mergeFrom.Metadata) {
					if (mergeTo.Metadata.TryGetValue(meta.Key, out var existing)) {
						// TODO: merge the metadata?
						Console.Error.WriteLine($"Duplicate metadata '{item.Key}.{meta.Key}'");
					} else {
						mergeTo.Metadata.Add(meta.Key, meta.Value);
					}
				}
			} else {
				d.Add(item.Key, item.Value);
			}
		}
	}

	public static MSBuildSchema? LoadBuiltInSchemas()
	{
		string thisFilePath = GetThisFilePath();
		var schemaSourceDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFilePath)!, "..", "MonoDevelop.MSBuild", "Schemas"));

		var schemas = TryLoadSchemasFromDirectory(schemaSourceDir) ?? LoadBuiltInSchemasFromResources();
		return CombineSchemas(schemas);
	}

	static string GetThisFilePath([CallerFilePath] string? thisFilePath = null) => thisFilePath!;

	static IEnumerable<MSBuildSchema>? TryLoadSchemasFromDirectory (string schemaDirectory)
	{
		if (!Directory.Exists(schemaDirectory)) {
			return null;
		}

		var schemas = new List<MSBuildSchema>();

		foreach (var schemaFile in Directory.GetFiles(schemaDirectory, "*.buildschema.json")) {
			using var reader = File.OpenText(schemaFile);
			try {
				var schema = MSBuildSchema.Load(reader, out var loadErrors, schemaFile);
				schemas.Add(schema);
				PrintSchemaErrors(loadErrors);
			} catch (JsonReaderException jex) {
				Console.Error.WriteLine($"{Path.GetFileName (schemaFile)}({jex.LineNumber}, {jex.LinePosition}): error: {jex.Message}'");
				continue;
			}
		}

		if (schemas.Count == 0) {
			Console.Error.WriteLine($"No built-in schemas found in '{schemaDirectory}'");
			return null;
		}

		return schemas;
	}

	static IEnumerable<MSBuildSchema> LoadBuiltInSchemasFromResources ()
	{
		foreach ((var schema, var errors) in MSBuildSchemaProvider.GetAllBuiltInSchemas()) {
			PrintSchemaErrors(errors);
			yield return schema;
		};
	}

	// this tool will be used when editing the schemas, so check they don't have errors
	static void PrintSchemaErrors (IEnumerable<MSBuildSchemaLoadError> errors)
	{
		foreach (var error in errors) {
			if (error is not null) {
				if (error.Origin is not null) {
					Console.Error.Write(Path.GetFileName(error.Origin));
					if (error.FilePosition is (int line, int col)) {
						Console.Error.Write($"({line}, {col}): ");
					}
				}
				Console.Error.WriteLine($"{error.Severity.ToString().ToLower()}: {error.Message}");
			}
		}
	}
}