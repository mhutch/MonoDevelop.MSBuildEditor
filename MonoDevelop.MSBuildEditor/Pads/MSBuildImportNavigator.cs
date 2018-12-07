// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.CodeAnalysis.Text;
using MonoDevelop.Core;
using MonoDevelop.MSBuildEditor.Language;
using Xwt;
using Xwt.Drawing;

namespace MonoDevelop.MSBuildEditor.Pads
{
	class MSBuildImportNavigator : TreeView
	{
		DocumentWithMimeTypeTracker documentTracker;
		readonly DataField<string> markupField = new DataField<string> ();
		readonly DataField<TextSpan> spanField = new DataField<TextSpan> ();
		readonly TreeStore store;

		public MSBuildImportNavigator ()
		{
			Columns.Add ("Text", new TextCellView { MarkupField = markupField });
			HeadersVisible = false;
			BorderVisible = false;

			store = new TreeStore (markupField, spanField);
			DataSource = store;

			documentTracker = new DocumentWithMimeTypeTracker ("application/x-msbuild");
			documentTracker.DocumentChanged += DocumentChanged;

			DocumentChanged (documentTracker, new DocumentChangedEventArgs (documentTracker.Document, null));
		}

		void DocumentChanged (object sender, DocumentChangedEventArgs e)
		{
			if (e.OldDocument != null) {
				e.OldDocument.DocumentParsed -= DocumentParsed;
			}
			if (e.NewDocument != null) {
				e.NewDocument.DocumentParsed += DocumentParsed;
			}
			DocumentParsed (e.NewDocument, EventArgs.Empty);
		}

		void DocumentParsed (object sender, EventArgs e)
		{
			Runtime.RunInMainThread ((Action)Update);
		}

		void Update ()
		{
			store.Clear ();

			if (documentTracker.Document?.ParsedDocument is MSBuildParsedDocument doc) {
				var shorten = DescriptionMarkupFormatter.CreateFilenameShortener (doc.Document.RuntimeInformation);
				AddNode (store.AddNode (), doc.Document, shorten);
				ExpandAll ();
			}
		}

		void AddNode (TreeNavigator treeNavigator, MSBuildDocument document, Func<string,(string prefix, string remaining)?> shorten)
		{
			bool first = true;

			string group = null;

			foreach (var import in document.Imports) {
				bool needsInsert = !first;
				first = false;

				void CloseGroup ()
				{
					if (group != null) {
						treeNavigator.MoveToParent ();
						group = null;
					}
				}

				if (import.OriginalImport.IndexOf('*') > -1 || import.OriginalImport[0] == '(') {
					if (import.OriginalImport != group) {
						CloseGroup ();
						if (needsInsert) {
							treeNavigator.InsertAfter ();
							needsInsert = false;
						}
						treeNavigator.SetValue (markupField, $"<span color='{Colors.BlueViolet.ToHexString ()}'>{GLib.Markup.EscapeText(import.OriginalImport)}</Span>");
						if (import.IsResolved) {
							group = import.OriginalImport;
							treeNavigator.AddChild ();
						} else {
							continue;
						}
					}
				} else {
					CloseGroup ();
				}

				if (needsInsert) {
					treeNavigator.InsertAfter ();
				}

				if (import.IsResolved) {
					var shortened = shorten (import.Filename);
					if (shortened.HasValue) {
						treeNavigator.SetValue (markupField, $"<span color='{Colors.Blue.ToHexString ()}'>{GLib.Markup.EscapeText (shortened.Value.prefix)}</span>{shortened.Value.remaining}");
					} else {
						treeNavigator.SetValue (markupField, GLib.Markup.EscapeText (import.Filename));
					}
				} else {
					treeNavigator.SetValue (markupField, $"<span color='{Colors.Red.ToHexString ()}'>{GLib.Markup.EscapeText (import.OriginalImport)}</span>");
				}

				if (import.IsResolved && import.Document.Imports.Count > 0) {
					treeNavigator.AddChild ();
					AddNode (treeNavigator, import.Document, shorten);
					treeNavigator.MoveToParent ();
				}
			}
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				if (documentTracker.Document != null) {
					documentTracker.Document.DocumentParsed -= DocumentParsed;
				}
				documentTracker?.Dispose ();
				documentTracker = null;
			}

			base.Dispose (disposing);
		}
	}
}