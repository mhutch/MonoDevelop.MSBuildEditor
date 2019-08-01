// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;

using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

using MonoDevelop.MSBuild.Editor.Host;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Export (typeof (IMSBuildEditorHost))]
	class VisualStudioMSBuildEditorHost : IMSBuildEditorHost
	{
		[Import]
		internal SVsServiceProvider ServiceProvider { get; set; }

		public void OpenFile (string destFile, int destOffset)
		{
			ThreadHelper.ThrowIfNotOnUIThread ();

			var openDoc = ServiceProvider.GetService (typeof (SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
			Assumes.Present (openDoc);

			var logViewGuid = VSConstants.LOGVIEWID.Code_guid;

			Check (openDoc.OpenDocumentViaProject (destFile, ref logViewGuid, out var _, out var _, out var _, out var frame));

			Check (frame.Show ());

			var textLinesGuid = typeof (IVsTextLines).GUID;
			Check (frame.QueryViewInterface (ref textLinesGuid, out var viewPtr));

			var textLines = (IVsTextLines)Marshal.GetUniqueObjectForIUnknown (viewPtr);
			if (textLines == null) {
				throw new Exception ("Did not get text lines for view");
			}

			var textMgr = ServiceProvider.GetService (typeof (SVsTextManager)) as IVsTextManager;
			Assumes.Present (textMgr);

			Check (textMgr.GetActiveView (0, textLines, out var textView));

			if (destOffset > -1) {
				Check (textView.GetLineAndColumn (destOffset, out var line, out var col));
				Check (textView.SetCaretPos (line, col));
			}

			void Check (int result)
			{
				if (result != VSConstants.S_OK) {
					throw new Exception ($"Unexpected result {result}");
				}
			}
		}

		//TODO: handle multiple paths
		public void ShowGoToDefinitionResults (string[] paths)
		{
			ThreadHelper.ThrowIfNotOnUIThread ();

			if (paths.Length == 1) {
				OpenFile (paths[0], -1);
			}
		}

		public void ShowStatusBarMessage (string v)
		{
			ThreadHelper.ThrowIfNotOnUIThread ();

			var statusBar = (IVsStatusbar)ServiceProvider.GetService (typeof (SVsStatusbar));
			Assumes.Present (statusBar);

			statusBar.SetText (v);
		}
	}
}
