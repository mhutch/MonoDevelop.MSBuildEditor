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

var builtInSchemas = MSBuildSchemaUtils.LoadBuiltInSchemas ();

var diff = MSBuildSchemaUtils.GetAddedOrChanged(builtInSchemas, xsdSchemaReader.Schema);

foreach (string intrinsicProp in new string[] { "MSBuildTreatWarningsAsErrors", "MSBuildWarningsAsErrors", "MSBuildWarningsAsMessages" }) {
	diff.Properties.Remove(intrinsicProp);
}

// I can't find targets from the WsdlXsd* properties anywhere, so don't bother trying to import them
diff.Properties.Keys.Where(k => k.StartsWith ("WsdlXsd", StringComparison.OrdinalIgnoreCase)).ToList().ForEach(k => diff.Properties.Remove(k));

using var writer = new MSBuildSchemaWriter(Console.Out);
writer.Write(diff);

return 0;
