// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace MonoDevelop.MSBuild.Editor.Host
{
	/// <summary>
	/// This is assumed to be used only from the UI thread
	/// </summary>
	public interface IMSBuildEditorHost
	{
		void ShowGoToDefinitionResults (string[] paths);
		bool OpenFile (string destFile, int destOffset, bool isPreview = false);
		void ShowStatusBarMessage (string v);
		Dictionary<string, ITextBuffer> GetOpenDocuments ();
	}
}