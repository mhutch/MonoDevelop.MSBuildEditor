// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.VisualStudio.Text.Editor;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Projects.Text;

namespace MonoDevelop.MSBuildEditor.Pads
{
	// shows documents with 
	class DocumentWithMimeTypeTracker : IDisposable
	{
		bool disposed;
		readonly string mimeType;

		//tracks the current active document, regardless of its mimetype
		Document activeDocument;

		/// <summary>
		/// The current active document, if it matches the mimetype, otherwise null.
		/// </summary>
		public Document Document { get; private set; }

		public DocumentWithMimeTypeTracker (string mimeType)
		{
			this.mimeType = mimeType;

			IdeApp.Workbench.ActiveDocumentChanged += ActiveDocumentChanged;
			ActiveDocumentChanged (null, null);
		}

		void ActiveDocumentChanged (object sender, EventArgs e)
		{
			if (activeDocument?.Editor != null) {
				activeDocument.Editor.MimeTypeChanged -= MimeTypeChanged;
			}

			activeDocument = IdeApp.Workbench.ActiveDocument;
			if (activeDocument?.Editor != null) {
				activeDocument.Editor.MimeTypeChanged += MimeTypeChanged;
			} else {
				activeDocument = null;
			}

			MimeTypeChanged (null, null);
		}

		void MimeTypeChanged (object sender, EventArgs e)
		{
			Document oldDoc = Document;

			if (activeDocument?.Editor?.MimeType == mimeType) {
				Document = activeDocument;
			} else {
				Document = null;
			}

			if (oldDoc != Document) {
				DocumentChanged?.Invoke (this, new DocumentChangedEventArgs (Document, oldDoc));
			}
		}

		public event EventHandler<DocumentChangedEventArgs> DocumentChanged;

		public void Dispose ()
		{
			if (disposed) {
				return;
			}
			disposed = true;

			IdeApp.Workbench.ActiveDocumentChanged -= ActiveDocumentChanged;
			if (activeDocument != null) {
				Document.Editor.MimeTypeChanged -= MimeTypeChanged;
			}
		}
	}

	class DocumentChangedEventArgs : EventArgs
	{
		public DocumentChangedEventArgs (
			Document newDocument,
			Document oldDocument)
		{
			NewDocument = newDocument;
			OldDocument = oldDocument;
		}

		public Document NewDocument { get; }
		public Document OldDocument { get; }
	}
}