// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Text;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	public static class MSBuildTextEditorExtensions
	{
		public static ITextSource GetTextSource (this ITextSnapshot snapshot, string filename = null) =>
			new SnapshotTextSource (snapshot, filename);
	}
}