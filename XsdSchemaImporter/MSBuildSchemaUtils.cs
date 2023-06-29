// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;

static class MSBuildSchemaUtils
{

	/// <summary>
	/// Returns a schema containing symbols from <c>other</c> that differ from those in the <c>basis</c>
	/// </summary>
	public static MSBuildSchema GetAddedOrChanged(MSBuildSchema basis, MSBuildSchema other)
	{
		var diff = new MSBuildSchema();

		foreach (var item in other.Items.Values) {
			if (item.Description.IsEmpty) {
				continue;
			}

			if (basis.Items.TryGetValue(item.Name, out var basisItem)
				&& basisItem.Description.Text == item.Description.Text
				&& item.Metadata.Values.All(otherMeta =>
					basisItem.Metadata.TryGetValue(otherMeta.Name, out var basisMeta)
					&& (otherMeta.Description.IsEmpty
						|| basisMeta.Description.Text == otherMeta.Description.Text))) {
				continue;
			}

			diff.Items.Add(item.Name, item);
		}

		foreach (var otherProp in other.Properties.Values) {
			if (otherProp.Description.IsEmpty) {
				continue;
			}

			if (basis.Properties.TryGetValue(otherProp.Name, out var basisProp)
				&& basisProp.Description.Text == otherProp.Description.Text) {
				continue;
			}

			diff.Properties.Add(otherProp.Name, otherProp);
		}

		foreach (var otherTask in other.Tasks.Values) {
			if (otherTask.Description.IsEmpty) {
				continue;
			}

			if (basis.Tasks.TryGetValue(otherTask.Name, out var basisTask)
				&& basisTask.Description.Text == otherTask.Description.Text) {
				continue;
			}

			diff.Tasks.Add(otherTask.Name, otherTask);
		}

		foreach (var target in other.Targets.Values) {
			if (target.Description.IsEmpty) {
				continue;
			}

			if (basis.Targets.TryGetValue(target.Name, out var existing)
				&& existing.Description.Text == target.Description.Text) {
				continue;
			}

			diff.Targets.Add(target.Name, target);
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

		var schemas = TryLoadSchemasFromDirectory(schemaSourceDir) ?? LoadBuiltinSchemasFromResources();
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
			var reader = File.OpenText(schemaFile);
			var schema = MSBuildSchema.Load(reader, out var loadErrors, schemaFile);
			schemas.Add(schema);
			PrintSchemaErrors(loadErrors);
		}

		if (schemas.Count == 0) {
			Console.Error.WriteLine($"No built-in schemas found in '{schemaDirectory}'");
			return null;
		}

		return schemas;
	}

	static IEnumerable<MSBuildSchema> LoadBuiltinSchemasFromResources ()
	{
		foreach ((var schema, var errors) in MSBuildSchemaProvider.GetAllBuiltinSchemas()) {
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
					if (error.FilePosition is (int line, _)) {
						Console.Error.Write($"({line}): "); ;
					}
				}
				Console.Error.WriteLine($"{error.Severity.ToString().ToLower()}: {error.Message}");
			}
		}
	}
}