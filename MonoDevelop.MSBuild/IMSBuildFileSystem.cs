// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

namespace MonoDevelop.MSBuild
{
	interface IMSBuildFileSystem
	{
		bool DirectoryExists (string basePath);
		IEnumerable<string> GetDirectories (string basePath);
		IEnumerable<string> GetFiles (string basePath);
	}

	class DefaultMSBuildFileSystem : IMSBuildFileSystem
	{
		DefaultMSBuildFileSystem ()
		{	
		}

		public bool DirectoryExists (string path) => Directory.Exists (path);

		public IEnumerable<string> GetDirectories (string path) => Directory.GetDirectories (path);

		public IEnumerable<string> GetFiles (string path) => Directory.GetFiles (path);

		public static DefaultMSBuildFileSystem Instance { get; } = new DefaultMSBuildFileSystem ();
	}
}
