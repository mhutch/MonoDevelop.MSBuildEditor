// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Ide.Editor;

namespace MonoDevelop.MSBuildEditor.Language
{
	interface IRegionAnnotation
	{
		DocumentRegion Region { get; }
	}

	public class NavigationAnnotation : IRegionAnnotation
	{
		public NavigationAnnotation (string path, DocumentRegion region)
		{
			Path = path;
			Region = region;
		}

		public DocumentRegion Region { get; }
		public string Path { get; }
	}
}
