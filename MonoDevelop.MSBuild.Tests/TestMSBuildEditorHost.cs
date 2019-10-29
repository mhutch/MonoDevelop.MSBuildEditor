// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;

using MonoDevelop.MSBuild.Editor.Host;

namespace MonoDevelop.MSBuild.Tests
{
	// this just needs to compose, it doesn't need to work
	[Export (typeof (IMSBuildEditorHost))]
	class TestMSBuildEditorHost : IMSBuildEditorHost
	{
		public Dictionary<string, ITextBuffer> GetOpenDocuments ()
		{
			throw new System.NotImplementedException ();
		}

		public bool OpenFile (string destFile, int destOffset, bool isPreview = false)
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

	[Export (typeof(IStreamingFindReferencesPresenter))]
	class TestStreamingFindReferencesPresenter : IStreamingFindReferencesPresenter
	{
		public void ClearAll () => throw new System.NotImplementedException ();

		public FindReferencesContext StartSearch (string title, string referenceName, bool showUsage)
			=> throw new System.NotImplementedException ();
	}
}
