// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;

// this is originally from MonoDevelop - MonoDevelop.Projects.MSBuild.MSBuildSdkResolver
namespace MonoDevelop.MSBuild.SdkResolution
{
	partial class MSBuildSdkResolver
	{
		class SdkResultImpl : SdkResult
		{
			public SdkResultImpl (SdkReference sdkReference, IEnumerable<string> errors, IEnumerable<string> warnings)
			{
				Success = false;
				Sdk = sdkReference;
				Errors = errors;
				Warnings = warnings;
			}

			public SdkResultImpl (SdkReference sdkReference, string path, string version,
				IDictionary<string, string> propertiesToAdd, IDictionary<string, SdkResultItem> itemsToAdd, IEnumerable<string> warnings)
			{
				Success = true;
				Sdk = sdkReference;
				PropertiesToAdd = propertiesToAdd;
				ItemsToAdd = itemsToAdd;
				Path = path;
				Version = version;
				Warnings = warnings;
			}

			public SdkResultImpl (SdkReference sdkReference, IEnumerable<string> paths, string version,
				IDictionary<string, string> propertiesToAdd, IDictionary<string, SdkResultItem> itemsToAdd, IEnumerable<string> warnings)
				: this (sdkReference, paths.FirstOrDefault(), version, propertiesToAdd, itemsToAdd, warnings)
			{
				if (paths.Count() > 1) {
					AdditionalPaths = paths.Skip (1).ToList ();
				}
			}

			public SdkReference Sdk { get; }
			public IEnumerable<string> Errors { get; }
			public IEnumerable<string> Warnings { get; }
		}
	}
}