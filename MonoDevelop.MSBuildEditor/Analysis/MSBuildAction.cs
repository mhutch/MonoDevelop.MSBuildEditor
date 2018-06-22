// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MonoDevelop.MSBuildEditor.Analysis
{
	abstract class MSBuildAction
	{
		public abstract string Title { get; }

		protected abstract Task<IEnumerable<MSBuildActionOperation>> ComputeOperationsAsync(CancellationToken cancellationToken);

		protected virtual Task<IEnumerable<MSBuildActionOperation>> ComputePreviewOperationsAsync(CancellationToken cancellationToken)
		{
			return ComputeOperationsAsync (cancellationToken);
		}
	}
}