// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MonoDevelop.Core;
using MonoDevelop.MSBuild.Language;
using Xwt;

namespace MonoDevelop.MSBuildEditor.Pads
{
	class MSBuildImportNavigator : TreeView
	{
		DocumentWithMimeTypeTracker documentTracker;
		readonly DataField<string> markupField = new DataField<string> ();
		readonly DataField<Import> importField = new DataField<Import> ();
		readonly DataField<bool> isGroupField = new DataField<bool> ();
		readonly TreeStore store;

		static readonly string colorGroup = "#2CC775";
		static readonly string colorReplacement = "#7DA7FF";
		static readonly string colorUnresolved = "#FF5446";

		public MSBuildImportNavigator ()
		{
			Columns.Add ("Text", new TextCellView { MarkupField = markupField });
			HeadersVisible = false;
			BorderVisible = false;

			store = new TreeStore (markupField, importField, isGroupField);
			DataSource = store;

			documentTracker = new DocumentWithMimeTypeTracker ("application/x-msbuild");
			documentTracker.DocumentChanged += DocumentChanged;

			DocumentChanged (documentTracker, new DocumentChangedEventArgs (documentTracker.Document, null));
		}

		void DocumentChanged (object sender, DocumentChangedEventArgs e)
		{
			if (e.OldDocument != null) {
				e.OldDocument.DocumentContext.DocumentParsed -= DocumentParsed;
			}
			if (e.NewDocument != null) {
				e.NewDocument.DocumentContext.DocumentParsed += DocumentParsed;
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
			if (documentTracker.Document?.DocumentContext.ParsedDocument is MSBuildParsedDocument doc) {
				var shorten = DisplayElementFactory.CreateFilenameShortener (doc.Document.RuntimeInformation);
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
						treeNavigator.SetValues (
							markupField, $"<span color='{colorGroup}'>{GLib.Markup.EscapeText(import.OriginalImport)}</Span>",
							importField, import,
							isGroupField, true
						);
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
						treeNavigator.SetValues (
							markupField, $"<span color='{colorReplacement}'>{GLib.Markup.EscapeText (shortened.Value.prefix)}</span>{shortened.Value.remaining}",
							importField, import,
							isGroupField, false
						);
					} else {
						treeNavigator.SetValues (
							markupField, GLib.Markup.EscapeText (import.Filename),
							importField, import,
							isGroupField, false
						);
					}
				} else {
					treeNavigator.SetValues (
						markupField, $"<span color='{colorUnresolved}'>{GLib.Markup.EscapeText (import.OriginalImport)}</span>",
						importField, import,
						isGroupField, false
					);
				}

				if (import.IsResolved && import.Document.Imports.Count > 0) {
					treeNavigator.AddChild ();
					AddNode (treeNavigator, import.Document, shorten);
					treeNavigator.MoveToParent ();
				}
			}
		}

		protected override void OnRowActivated (TreeViewRowEventArgs a)
		{
			var nav = store.GetNavigatorAt (a.Position);
			if (nav != null) {
				var isGroup = nav.GetValue (isGroupField);
				if (!isGroup) {
					var import = nav.GetValue (importField);
					Ide.IdeApp.Workbench.OpenDocument (import.Filename, null, true);
				}
			}
			base.OnRowActivated (a);
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				if (documentTracker.Document != null) {
					documentTracker.Document.DocumentContext.DocumentParsed -= DocumentParsed;
				}
				documentTracker?.Dispose ();
				documentTracker = null;
			}

			base.Dispose (disposing);
		}
	}
}