// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using MonoDevelop.MSBuildEditor.Language;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class MSBuildSchemaProvider
	{
		public virtual MSBuildSchema GetSchema (Import import)
		{
			var isCommonTargets = string.Equals (Path.GetFileName (import.Filename), "Microsoft.Common.targets", System.StringComparison.OrdinalIgnoreCase);
			if (isCommonTargets) {
				return MSBuildSchema.LoadResource ("MonoDevelop.MSBuildEditor.Schemas.CommonTargets.buildschema.json");
			}

			if (import.IsResolved) {
				string filename = import.Filename + ".buildschema.json";
				if (File.Exists (filename)) {
					using (var reader = File.OpenText (filename)) {
						return MSBuildSchema.Load (reader);
					}
				}
			}
			return null;
		}
	}
}
