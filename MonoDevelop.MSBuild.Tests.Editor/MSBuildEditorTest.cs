// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using System.Threading;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.Xml.Editor.Tests;

using MonoDevelop.MSBuild.Editor;
using MonoDevelop.MSBuild.Editor.Analysis;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Editor.CodeActions;
using System.Linq;

namespace MonoDevelop.MSBuild.Tests
{
	public abstract class MSBuildEditorTest : EditorTest
	{
		public new MSBuildEditorCatalog Catalog => (MSBuildEditorCatalog)base.Catalog;

		protected override EditorCatalog CreateCatalog () => MSBuildTestEnvironment.CreateEditorCatalog ();

		protected override string ContentTypeName => MSBuildContentType.Name;

		// TODO: use a custom parser provider that limits the analyzers
		internal MSBuildBackgroundParser GetParser (ITextBuffer buffer) => Catalog.MSBuildParserProvider.GetParser (buffer);
	}

	internal static class MSBuildEditorTestExtensions
	{
		public static async Task ApplyCodeAction (this ITextView textView, MSBuildCodeAction action, CancellationToken cancellationToken = default)
		{
			var workspaceEdit = await action.ComputeOperationsAsync (cancellationToken);
			// TODO: check all edits are for the textView
			var edits = workspaceEdit.Operations.OfType<MSBuildDocumentEdit> ().SelectMany(d => d.TextEdits).ToList ();
			edits.Apply(textView.TextBuffer, cancellationToken, textView);
		}
	}
}
