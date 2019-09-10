// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using MonoDevelop.MSBuild.Editor.Host;
using MonoDevelop.Projects;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Export (typeof (IMSBuildEditorHost))]
	class MonoDevelopMSBuildEditorHost : IMSBuildEditorHost
	{
		public async void OpenFile (string destFile, int destOffset)
		{
			Runtime.AssertMainThread ();

			try {
				var doc = await IdeApp.Workbench.OpenDocument (destFile, (Project)null, OpenDocumentOptions.Default);
				var textView = await doc.GetContentWhenAvailable<ITextView> ();
				if (textView != null) {
					textView.Caret.MoveTo (new SnapshotPoint (textView.TextSnapshot, destOffset));
					textView.Caret.EnsureVisible ();
				}
			} catch (Exception ex) {
				LoggingService.LogError ("Error opening file", ex);
			}
		}

		//TODO: handle multiple paths
		public void ShowGoToDefinitionResults (string[] paths)
		{
			Runtime.AssertMainThread ();

			if (paths.Length == 1) {
				OpenFile (paths[0], -1);
			}
		}

		public void ShowStatusBarMessage (string v)
		{
			Runtime.AssertMainThread ();

			IdeApp.Workbench.StatusBar.ShowMessage (v);
		}
	}
}
