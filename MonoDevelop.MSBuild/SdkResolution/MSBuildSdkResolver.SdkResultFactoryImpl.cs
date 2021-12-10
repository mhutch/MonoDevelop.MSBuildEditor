// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Framework;

// this is originally from MonoDevelop - MonoDevelop.Projects.MSBuild.MSBuildSdkResolver
namespace MonoDevelop.MSBuild.SdkResolution
{
	partial class MSBuildSdkResolver
	{
		internal class SdkResultFactoryImpl : SdkResultFactory
		{
			readonly SdkReference _sdkReference;

			internal SdkResultFactoryImpl (SdkReference sdkReference)
			{
				_sdkReference = sdkReference;
			}

			public override SdkResult IndicateSuccess (string path, string version, IEnumerable<string> warnings = null)
			{
				return new SdkResultImpl (_sdkReference, path, version, null, null, warnings);
			}

			public override SdkResult IndicateFailure (IEnumerable<string> errors, IEnumerable<string> warnings = null)
			{
				return new SdkResultImpl (_sdkReference, errors, warnings);
			}

			public override SdkResult IndicateSuccess (IEnumerable<string> paths, string version, IDictionary<string, string> propertiesToAdd = null, IDictionary<string, SdkResultItem> itemsToAdd = null, IEnumerable<string> warnings = null)
			{
				return new SdkResultImpl (_sdkReference, paths, version, propertiesToAdd, itemsToAdd, warnings);
			}

			public override SdkResult IndicateSuccess (string path, string version, IDictionary<string, string> propertiesToAdd, IDictionary<string, SdkResultItem> itemsToAdd, IEnumerable<string> warnings = null)
			{
				return new SdkResultImpl (_sdkReference, path, version, propertiesToAdd, itemsToAdd, warnings);
			}
		}
	}
}