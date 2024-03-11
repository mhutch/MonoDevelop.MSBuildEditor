// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Tests.Helpers
{
	class TestSchemaProvider : MSBuildSchemaProvider
	{
		readonly Dictionary<(string filename, string sdk), MSBuildSchema> schemas = new ();

		public void AddTestSchema (string filename, string sdk, MSBuildSchema schema)
		{
			schemas.Add ((filename, sdk), schema);
		}

		public override MSBuildSchema GetSchema (string path, string sdk, out IList<MSBuildSchemaLoadError> loadErrors)
		{
			if (schemas.TryGetValue ((Path.GetFileName (path), sdk), out MSBuildSchema schema)) {
				loadErrors = null;
				return schema;
			}

			return base.GetSchema (path, sdk, out loadErrors);
		}

		public override ICollection<MSBuildSchema> GetFallbackSchemas () => [];
	}
}
