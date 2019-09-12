// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using MonoDevelop.MSBuild.Editor.Host;

namespace MonoDevelop.MSBuild.Tests
{
	// this just needs to compose, it doesn't need to work
	[Export (typeof (IMSBuildEditorHost))]
	class TestMSBuildEditorHost : IMSBuildEditorHost
	{
		public void OpenFile (string destFile, int destOffset)
		{
			throw new System.NotImplementedException ();
		}

		public void ShowGoToDefinitionResults (string[] paths)
		{
			throw new System.NotImplementedException ();
		}

		public void ShowStatusBarMessage (string v)
		{
			throw new System.NotImplementedException ();
		}
	}
}
