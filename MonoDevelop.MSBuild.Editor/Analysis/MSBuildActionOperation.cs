// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	abstract class MSBuildActionOperation
	{
		public abstract void Apply (IEditorOptions options, ITextBuffer document, CancellationToken cancellationToken);
	}
}