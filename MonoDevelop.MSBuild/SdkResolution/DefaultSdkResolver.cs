// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.Build.Framework;

//ported from MonoDevelop.Projects.MSBuild.DefaultSdkResolver
namespace MonoDevelop.MSBuild.SdkResolution
{
	/// <summary>
	///     Default SDK resolver for compatibility with VS2017 RTM.
	/// <remarks>
	///     Default Sdk folder will to:
	///         1) MSBuildSDKsPath environment variable if defined
	///         2) When in Visual Studio, (VSRoot)\MSBuild\Sdks\
	///         3) Outside of Visual Studio (MSBuild Root)\Sdks\
	/// </remarks>
	/// </summary>
	internal class DefaultSdkResolver : SdkResolver
	{
		public DefaultSdkResolver (string sdksPath) => SdksPath = sdksPath;

		public override string Name => "DefaultSdkResolver";

		public override int Priority => 10000;

		public string SdksPath { get; }

		public override SdkResult Resolve (SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
		{
			var sdkPath = Path.Combine (SdksPath, sdkReference.Name, "Sdk");

			// Note: On failure MSBuild will log a generic message, no need to indicate a failure reason here.
			return Directory.Exists (sdkPath)
				? factory.IndicateSuccess (sdkPath, string.Empty)
				: factory.IndicateFailure (null);
		}
	}
}