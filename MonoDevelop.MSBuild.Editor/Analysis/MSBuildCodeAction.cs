// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	/// <summary>
	/// A user-invokable action that modifies MSBuild code by performing a set of <see cref="MSBuildCodeActionOperation" /> operations.
	/// </summary>
	abstract class MSBuildCodeAction

	{
		public abstract string Title { get; }

		public abstract Task<IEnumerable<MSBuildCodeActionOperation>> ComputeOperationsAsync (CancellationToken cancellationToken);

		public virtual Task<IEnumerable<MSBuildCodeActionOperation>> ComputePreviewOperationsAsync (CancellationToken cancellationToken)
		{
			return ComputeOperationsAsync (cancellationToken);
		}
	}

	/// <summary>
	/// Convenience base class for a <see cref="MSBuildCodeAction" /> that consists of a single <see cref="MSBuildCodeActionOperation" />
	/// </summary>
	abstract class SimpleMSBuildCodeAction : MSBuildCodeAction
	{
		public sealed override Task<IEnumerable<MSBuildCodeActionOperation>> ComputeOperationsAsync (CancellationToken cancellationToken)
			=> Task.FromResult<IEnumerable<MSBuildCodeActionOperation>> (new MSBuildCodeActionOperation[] { CreateOperation () });

		protected abstract MSBuildCodeActionOperation CreateOperation ();
	}
}