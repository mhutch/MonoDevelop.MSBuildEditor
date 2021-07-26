// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;

namespace MonoDevelop.MSBuild.SdkResolution
{
	public class SdkInfo
	{
		internal SdkInfo (string name, SdkResult result)
		{
			Name = name ?? throw new ArgumentNullException (nameof (name));
			Version = result.Version;
			ItemsToAdd = result.ItemsToAdd;
			PropertiesToAdd = result.PropertiesToAdd;

			// Paths and AdditionalPaths are only separate to preserve MSBuild binary compat
			// but we can recombine them to make it easier for our internal consumers

			var additionalPathsOffset = result.Path != null ? 1 : 0;

			var paths = new string[additionalPathsOffset + (result.AdditionalPaths?.Count ?? 0)];

			if (result.Path != null) {
				paths[0] = result.Path;
			}

			if (result.AdditionalPaths != null && result.AdditionalPaths.Count > 0) {
				for(int i = 0; i < result.AdditionalPaths.Count; i++) {
					paths[i + additionalPathsOffset] = result.AdditionalPaths[i];
				}
			}

			Paths = paths;
		}

		public SdkInfo (string name, string version, string path)
			: this (name, version, new[] { path })
		{
		}

		public SdkInfo (string name, string version, IList<string> paths)
		{
			Name = name ?? throw new ArgumentNullException (nameof (name));
			Version = version;
			Paths = paths ?? Array.Empty<string>();
		}

		public string Name { get; }
		public string Version { get; }

		/// <summary>
		/// The SDK path(s). May be empty e.g. for WorkloadAutoImportPropsLocator, but will not be null.
		/// </summary>
		public IList<string> Paths { get; }

		/// <summary>
		/// Returns the first path, or null if none are defined.
		/// </summary>
		public string Path => Paths is not null && Paths.Count > 0 ? Paths[0] : null;

		public IDictionary<string, SdkResultItem> ItemsToAdd { get; }
		public IDictionary<string, string> PropertiesToAdd { get; }
	}
}
