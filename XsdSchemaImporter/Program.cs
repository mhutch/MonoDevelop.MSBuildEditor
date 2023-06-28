// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Xml.Schema;

using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;

using SchemaImporter;

// This tool downloads the MSBuild XSD schema and prints out the
// symbols from the XSD that differ from the builtin schemas
// so that they can be reviewed and updated.
//
// NOTE: The diff is currently fairly crude and only finds
// symbols that have descriptions in the XSD and either do not
// exist in the builtin schemas or have a different description.
//
// NOTE: This does not yet read task parameters from the XSD.

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, _) => cts.Cancel();

string msbuildRepo = "https://raw.githubusercontent.com/dotnet/msbuild/main";
string xsdUrlCommonTypes = $"{msbuildRepo}/src/MSBuild/MSBuild/Microsoft.Build.CommonTypes.xsd";
string xsdUrlCore = $"{msbuildRepo}/src/MSBuild/MSBuild/Microsoft.Build.Core.xsd";

var cacheDir = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()!.Location)!, "Cache");

var downloadCache = new DownloadCache(cacheDir) {
	// ForceRefresh = true
};

var xsdStream = await downloadCache.GetStream(xsdUrlCommonTypes, cts.Token);
if (xsdStream is null) {
	Console.Error.WriteLine($"Error downloading file '{xsdUrlCommonTypes}'");
	return 2;
};
var commonTypesSchema = XmlSchema.Read(xsdStream, null)!;

var schemaSet = new XmlSchemaSet {
	XmlResolver = new ConstrainedXmlResolver(
		(uri) => downloadCache.GetStream(uri.ToString(), cts.Token).Result,
		(uri) => Console.Error.WriteLine($"Blocked resolving '{uri}' in schema loader")
		) {
			{ xsdUrlCore },
			{ xsdUrlCommonTypes }
		}
};
schemaSet.Add(MSBuildXsdSchemaReader.MSBuildSchemaUri, xsdUrlCommonTypes);
schemaSet.Compile();

var xsdSchemaReader = new MSBuildXsdSchemaReader();
xsdSchemaReader.Read(schemaSet);

var builtInSchemas = LoadBuiltInSchemas ();

var diff = GetAddedOrChanged(builtInSchemas, xsdSchemaReader.Schema);

using var writer = new MSBuildSchemaWriter(Console.Out);
writer.Write(diff);

return 0;

static void AddRange<K, V>(
	Dictionary<K, V> d,
	IEnumerable<KeyValuePair<K, V>> range)
	where K: notnull
{
	foreach (var kv in range) {
		d.Add(kv.Key, kv.Value);
	}
}

static void AddRangeOfItems (
	Dictionary<string, ItemInfo> d,
	IEnumerable<KeyValuePair<string, ItemInfo>> range)
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
					Console.Error.WriteLine ($"Duplicate metadata '{item.Key}.{meta.Key}'");
				} else {
					mergeTo.Metadata.Add(meta.Key, meta.Value);
				}
			}
		} else {
			d.Add(item.Key, item.Value);
		}
	}
}

static MSBuildSchema LoadBuiltInSchemas([CallerFilePath]string? thisFilePath = null)
{
	var schemas = new List<MSBuildSchema>();
	var errors = new List<MSBuildSchemaLoadError>();

	var dir = Path.GetFullPath (Path.Combine(Path.GetDirectoryName(thisFilePath)!, "..", "MonoDevelop.MSBuild", "Schemas"));
	foreach (var schemaFile in Directory.GetFiles(dir, "*.buildschema.json")) {
		var reader = File.OpenText(schemaFile);
		var schema = MSBuildSchema.Load(reader, out var loadErrors, schemaFile);
		schemas.Add(schema);
		errors.AddRange(loadErrors);
	}

	if (schemas.Count == 0) {
		Console.Error.WriteLine($"No built-in schemas found in '{dir}'");
	}

	// check for errors as it's likely we have edited the schemas since last running this tool
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

	return CombineSchemas (schemas);
}

static MSBuildSchema CombineSchemas(IEnumerable<MSBuildSchema> schemas)
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

/// <summary>
/// Returns a schema containing symbols from <c>other</c> that differ from those in the <c>basis</c>
/// </summary>
static MSBuildSchema GetAddedOrChanged (MSBuildSchema basis, MSBuildSchema other)
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