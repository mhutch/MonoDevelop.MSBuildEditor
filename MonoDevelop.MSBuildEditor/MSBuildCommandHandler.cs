// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using System.IO;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.MSBuild.Editor;
using MonoDevelop.TextEditor;

namespace MonoDevelop.MSBuildEditor
{
	[Export (typeof (IEditorContentProvider))]
	[ContentType (MSBuildContentType.Name)]
	[TextViewRole (PredefinedTextViewRoles.PrimaryDocument)]
	sealed class MSBuildCommandHandlerContentProvider : EditorContentInstanceProvider<MSBuildCommandHandler>
	{
		[ImportingConstructor]
		public MSBuildCommandHandlerContentProvider (ITextDocumentFactoryService textDocumentFactory)
		{
			TextDocumentFactory = textDocumentFactory;
		}

		public ITextDocumentFactoryService TextDocumentFactory { get; }

		protected override MSBuildCommandHandler CreateInstance (ITextView view) => new (view, this);
	}

	class MSBuildCommandHandler
	{
		readonly ITextView view;
		readonly MSBuildCommandHandlerContentProvider provider;

		public MSBuildCommandHandler (ITextView view, MSBuildCommandHandlerContentProvider provider)
		{
			this.view = view;
			this.provider = provider;
		}

		string TryGetFilename () =>
						provider.TextDocumentFactory.TryGetTextDocument (view.TextBuffer, out var doc)
										? doc.FilePath
										: null;

		[CommandHandler (DesignerSupport.Commands.SwitchBetweenRelatedFiles)]
		protected void Run ()
		{
			var counterpart = GetCounterpartFile ();
			IdeApp.Workbench.OpenDocument (counterpart, null, true);
		}

		[CommandUpdateHandler (DesignerSupport.Commands.SwitchBetweenRelatedFiles)]
		protected void Update (CommandInfo info)
		{
			info.Enabled = GetCounterpartFile () != null;
		}

		string GetCounterpartFile ()
		{
			var name = TryGetFilename ();
			if (name == null) {
				return null;
			}

			switch (Path.GetExtension (name.ToLower ())) {
			case ".targets":
				name = Path.ChangeExtension (name, ".props");
				break;
			case ".props":
				name = Path.ChangeExtension (name, ".targets");
				break;
			default:
				return null;
			}

			return File.Exists (name) ? name : null;
		}
	}
}