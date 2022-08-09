// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	/// <summary>
	/// Encapsulates an operation that modifies MSBuild code
	/// </summary>
	abstract class MSBuildCodeActionOperation
	{
		public abstract void Apply (IEditorOptions options, ITextBuffer document, CancellationToken cancellationToken, ITextView textView = null);
	}
}