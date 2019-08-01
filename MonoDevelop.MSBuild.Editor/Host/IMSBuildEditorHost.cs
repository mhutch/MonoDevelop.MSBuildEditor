// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Editor.Host
{
	public interface IMSBuildEditorHost
	{
		void ShowGoToDefinitionResults (string[] paths);
		void OpenFile (string destFile, int destOffset);
		void ShowStatusBarMessage (string v);
	}
}