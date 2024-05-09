// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

// avoid accidentally referencing other types from Microsoft.Build.Framework
using SdkReference = Microsoft.Build.Framework.SdkReference;

namespace MonoDevelop.MSBuild
{
	// avoid exposing Microsoft.Build.Framework dependency in public APIs
	public readonly struct MSBuildSdkReference
	{
		readonly SdkReference sdkReference;

		internal MSBuildSdkReference (SdkReference sdkReference)
		{
			this.sdkReference = sdkReference;
		}

		public MSBuildSdkReference (string name, string? version = null, string? minVersion = null)
		{
			this.sdkReference = new SdkReference (name, version, minVersion);
		}

		public string Name => sdkReference.Name;
		public string Version => sdkReference.Version;
		public string MinimumVersion => sdkReference.MinimumVersion;

		public override string ToString () => sdkReference.ToString ();

		public static bool TryParse (string sdk, out MSBuildSdkReference sdkReference)
		{
			if (SdkReference.TryParse (sdk, out var parsedSdk)) {
				sdkReference = new MSBuildSdkReference (parsedSdk);
				return true;
			}
			sdkReference = default;
			return false;
		}

		public SdkReference AsSdkReference () => sdkReference;
	}
}
