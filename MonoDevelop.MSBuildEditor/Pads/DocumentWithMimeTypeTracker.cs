// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.VisualStudio.Text.Editor;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.MSBuildEditor.Pads
{
	// copied from AddinMaker
	class ActiveEditorTracker : IDisposable
	{
		bool disposed;

		public ITextView TextView { get; private set; }
		public Document Document { get; private set; }

		public ActiveEditorTracker ()
		{
			IdeApp.Workbench.ActiveDocumentChanged += ActiveDocumentChanged;
			ActiveDocumentChanged (null, null);
		}

		void ActiveDocumentChanged (object sender, EventArgs e)
		{
			if (Document != null) {
				Document.ContentChanged -= ActiveContentChanged;
			}
			var oldActiveDocument = Document;
			Document = IdeApp.Workbench.ActiveDocument;

			if (Document != null) {
				Document.ContentChanged += ActiveContentChanged;
			}

			ActiveContentChanged (null, null, oldActiveDocument);
		}

		void ActiveContentChanged (object sender, EventArgs e)
		{
			ActiveContentChanged (sender, e, Document);
		}

		void ActiveContentChanged (object sender, EventArgs e, Document oldDocument)
		{
			//FIXME there doesn't seem to be a better way to determine whether the view is an editor
			//or to pull out the focused view when it's split e.g. diff view
			var oldView = TextView;
			if (Document?.GetContent<ITextView> () is ITextView view) {
				TextView = view;
			} else {
				TextView = null;
			}

			if (TextView != oldView) {
				ActiveEditorChanged?.Invoke (this, new ActiveEditorChangedEventArgs (TextView, oldView, Document, oldDocument));
			}
		}

		public event EventHandler<ActiveEditorChangedEventArgs> ActiveEditorChanged;

		public void Dispose ()
		{
			if (disposed) {
				return;
			}
			disposed = true;

			IdeApp.Workbench.ActiveDocumentChanged -= ActiveDocumentChanged;
			if (Document != null) {
				Document.ContentChanged -= ActiveContentChanged;
			}
		}
	}

	class ActiveEditorChangedEventArgs : EventArgs
	{
		public ActiveEditorChangedEventArgs (
			ITextView newView,
			ITextView oldView,
			Document newDocument,
			Document oldDocument)
		{
			NewView = newView;
			OldView = oldView;
			NewDocument = newDocument;
			OldDocument = oldDocument;
		}

		public ITextView NewView { get; }
		public ITextView OldView { get; }

		public Document NewDocument { get; }
		public Document OldDocument { get; }
	}
}