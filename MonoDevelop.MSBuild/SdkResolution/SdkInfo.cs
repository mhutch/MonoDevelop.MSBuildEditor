// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.Build.Framework;

namespace MonoDevelop.MSBuild.SdkResolution
{
	public class SdkInfo
	{
		internal SdkInfo (string name, SdkResult result)
		{
			Name = name;
			Version = result.Version;
			Path = result.Path;
			AdditionalPaths = result.AdditionalPaths;
			ItemsToAdd = result.ItemsToAdd;
			PropertiesToAdd = result.PropertiesToAdd;
		}

		public SdkInfo (string name, string version, string path)
		{
			Name = name;
			Version = version;
			Path = path;
		}

		public string Name { get; }
		public string Version { get; }
		public string Path { get; }

		public IList<string> AdditionalPaths { get; }
		public IDictionary<string, SdkResultItem> ItemsToAdd { get; }
		public IDictionary<string, string> PropertiesToAdd { get; }
	}
}
