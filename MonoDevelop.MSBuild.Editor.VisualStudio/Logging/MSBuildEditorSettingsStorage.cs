// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.IO;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Logging;

class MSBuildEditorSettingsStorage
{
	const string editorDataDirectoryName = "MSBuildEditor";

	readonly string roamingDataDir;
	readonly string localDataDir;

	public MSBuildEditorSettingsStorage ()
	{
		localDataDir = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), editorDataDirectoryName);
		roamingDataDir = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), editorDataDirectoryName);
	}

	public string RoamingDataDir => roamingDataDir; 
	public string LocalDataDir => localDataDir;
}
