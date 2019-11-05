// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	abstract class MSBuildAction
	{
		public abstract string Title { get; }

		public abstract Task<IEnumerable<MSBuildActionOperation>> ComputeOperationsAsync (CancellationToken cancellationToken);

		public virtual Task<IEnumerable<MSBuildActionOperation>> ComputePreviewOperationsAsync (CancellationToken cancellationToken)
		{
			return ComputeOperationsAsync (cancellationToken);
		}
	}

	abstract class SimpleMSBuildAction : MSBuildAction
	{
		public sealed override Task<IEnumerable<MSBuildActionOperation>> ComputeOperationsAsync (CancellationToken cancellationToken)
			=> Task.FromResult<IEnumerable<MSBuildActionOperation>> (new MSBuildActionOperation[] { CreateOperation () });

		protected abstract MSBuildActionOperation CreateOperation ();
	}
}