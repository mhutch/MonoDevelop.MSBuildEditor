// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Analysis;

namespace MonoDevelop.MSBuild.Editor.CodeActions
{
	/// <summary>
	/// A user-invokable action that modifies MSBuild code by performing an <see cref="MSBuildWorkspaceEdit" /> operations.
	/// </summary>
	abstract class MSBuildCodeAction
	{
		public abstract string Title { get; }

		public virtual IReadOnlyList<MSBuildDiagnostic> FixesDiagnostics => [];

		public virtual MSBuildCodeActionKind Kind => FixesDiagnostics.Count > 0
			? MSBuildCodeActionKind.CodeFix
			: MSBuildCodeActionKind.Refactoring;

		/// <summary>
		/// IDEs that care about <see cref="MSBuildCodeActionKind.ErrorFix"/> can use this to determine whether this
		/// action fixes error diagnostics. If so, they can use <see cref="MSBuildCodeActionKind.ErrorFix"/> as the
		/// effective kind, and ignore the <see cref="Kind"/> property.
		/// </summary>
		public bool GetFixesErrorDiagnostics () => FixesDiagnostics.Count > 0 && FixesDiagnostics.Any (d => d.Descriptor.Severity == MSBuildDiagnosticSeverity.Error);

		public abstract Task<MSBuildWorkspaceEdit> ComputeOperationsAsync (CancellationToken cancellationToken);
	}
}