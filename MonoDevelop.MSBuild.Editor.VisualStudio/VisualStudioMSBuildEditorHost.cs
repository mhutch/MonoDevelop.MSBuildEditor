// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;

using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

using MonoDevelop.MSBuild.Editor.Host;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Export (typeof (IMSBuildEditorHost))]
	class VisualStudioMSBuildEditorHost : IMSBuildEditorHost
	{
		[Import]
		internal SVsServiceProvider ServiceProvider { get; set; }

		[Import] IVsEditorAdaptersFactoryService EditorAdapter { get; set; }

		public bool OpenFile (string destFile, int destOffset, bool isPreview = false)
		{
			ThreadHelper.ThrowIfNotOnUIThread ();

			var openDoc = ServiceProvider.GetService<SVsUIShellOpenDocument, IVsUIShellOpenDocument> (true);

			var logViewGuid = VSConstants.LOGVIEWID.Code_guid;

			Check (openDoc.OpenDocumentViaProject (destFile, ref logViewGuid, out var _, out var _, out var _, out var frame));

			Check (frame.Show ());

			var textLinesGuid = typeof (IVsTextLines).GUID;
			Check (frame.QueryViewInterface (ref textLinesGuid, out var viewPtr));

			var textLines = (IVsTextLines)Marshal.GetUniqueObjectForIUnknown (viewPtr);
			if (textLines == null) {
				throw new Exception ("Did not get text lines for view");
			}

			var textMgr = ServiceProvider.GetService<SVsTextManager, IVsTextManager> ();

			Check (textMgr.GetActiveView (0, textLines, out var textView));

			if (destOffset > -1) {
				Check (textView.GetLineAndColumn (destOffset, out var line, out var col));
				Check (textView.SetCaretPos (line, col));
			}

			return true;
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

			var statusBar = ServiceProvider.GetService<SVsStatusbar, IVsStatusbar> (true);
			Assumes.Present (statusBar);

			statusBar.SetText (v);
		}

		public Dictionary<string,ITextBuffer> GetOpenDocuments ()
		{
			ThreadHelper.ThrowIfNotOnUIThread ();

			var rdt = ServiceProvider.GetService<SVsRunningDocumentTable, IVsRunningDocumentTable> (true);
			var rdt4 = ServiceProvider.GetService<SVsRunningDocumentTable, IVsRunningDocumentTable4> (true);

			Check (rdt.GetRunningDocumentsEnum (out var runningDocuments));

			var documents = new Dictionary<string, ITextBuffer> ();

			var cookies = new uint[64];
			while (Check (runningDocuments.Next ((uint)cookies.Length, cookies, out var fetched)) && fetched > 0) {
				for (int i = 0; i < fetched; i++) {
					uint cookie = cookies[i];
					var moniker = rdt4.GetDocumentMoniker (cookie);
					if (string.IsNullOrEmpty (moniker)) {
						continue;
					}
					//object cast avoids unnecessary dynamic code
					if ((object)rdt4.GetDocumentData (cookie) is IVsTextBuffer bufferAdapter) {
						var buffer = EditorAdapter.GetDataBuffer (bufferAdapter);
						if (buffer != null) {
							documents.Add (moniker, buffer);
						}
					}
				}
			}

			return documents;
		}

		bool Check (int result)
		{
			if (result == VSConstants.S_FALSE) {
				return false;
			}
			if (result == VSConstants.S_OK) {
				return true;
			}
			throw new Exception ($"Unexpected result {result}");
		}
	}
}
