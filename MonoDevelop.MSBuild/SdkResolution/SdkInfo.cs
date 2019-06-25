// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.SdkResolution
{
	public class SdkInfo
	{
		public SdkInfo (string name, SdkVersion version, string path)
		{
			Name = name;
			Version = version;
			Path = path;
		}

		public string Name { get; }
		public SdkVersion Version { get; }
		public string Path { get; }
	}

	public class SdkVersion
	{
	}
}
