// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Collections.Generic;
using System.IO;

using Microsoft.Extensions.Logging;

namespace MonoDevelop.MSBuild.Schema
{
	class MSBuildSchemaProvider
	{
		public MSBuildSchema? GetSchema (string path, string sdk, ILogger logger)
		{
			var schema = GetSchema (path, sdk, out var loadErrors);

			// FIXME: log which schema the error came from
			if (loadErrors != null) {
				using var logScope = logger.BeginScope (path);
				foreach (var error in loadErrors) {
					var logLevel = error.Severity == Xml.Analysis.XmlDiagnosticSeverity.Warning ? LogLevel.Warning : LogLevel.Error;
					if (logger.IsEnabled (logLevel)) {
						logger.Log (logLevel, schemaLoadErrorId, null, error.Message);
					}
				}
			}

			return schema;
		}

		public virtual MSBuildSchema? GetSchema (string path, string sdk, out IList<MSBuildSchemaLoadError>? loadErrors)
		{
			string filename = path + ".buildschema.json";
			if (File.Exists (filename)) {
				using var reader = File.OpenText (filename);
				return MSBuildSchema.Load (reader, out loadErrors, filename);
			}

			return BuiltInSchema.TryLoadForFile (path, sdk, out loadErrors);
		}

		static readonly EventId schemaLoadErrorId = new (0, "SchemaLoadError");
	}
}
