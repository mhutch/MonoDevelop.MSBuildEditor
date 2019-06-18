// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using Microsoft.VisualStudio.Text;

namespace MonoDevelop.MSBuild.Analysis
{
	abstract class MSBuildActionOperation
	{
		public abstract void Apply(ITextBuffer document, CancellationToken cancellationToken);
	}
}