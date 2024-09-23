// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Threading.Tasks;

namespace MonoDevelop.MSBuild.Editor.CodeActions
{
	/// <summary>
	/// A code action may be a code fix that fixes one or more diagnostics, or a refactoring that transforms code.
	/// </summary>
	abstract class MSBuildCodeActionProvider
	{
		public abstract Task RegisterCodeActionsAsync (MSBuildCodeActionContext context);

		/// <summary>
		/// If the provider produces actions that fix diagnostics, this should indicate the diagnostic(s) it is able to fix.
		/// </summary>
		/// <remarks>
		/// If this is non-empty, the provider will only be run at locations that include at least one of these diagnostics.
		/// </remarks>
		public virtual ImmutableArray<string> FixableDiagnosticIds => [];

		/// <summary>
		/// The CodeActionKind values that may be included on actions produced by this provider. By default, this is
		/// <see cref="MSBuildCodeActionKind.CodeFix"/> if <see cref="FixableDiagnosticIds"/> is non-empty, otherwise
		/// it is <see cref="MSBuildCodeActionKind.Refactoring"/>.
		/// </summary>
		/// <remarks>
		/// This provider will only be run when the IDE requests actions of these kind. If the provider produces actions
		/// of multiple kinds, it should include all of them here, and check <see cref="MSBuildCodeActionContext.RequestedActionKinds"/>
		/// before producing actions of each kind. Any extra actions produced that do not match the request will be discarded.
		/// </remarks>
		public virtual ImmutableArray<MSBuildCodeActionKind> ProducedCodeActionKinds
			=> IsCodeFix
				? [MSBuildCodeActionKind.CodeFix ]
				: [ MSBuildCodeActionKind.Refactoring ];

		public bool IsRefactoring => FixableDiagnosticIds.Length == 0;

		public bool IsCodeFix => FixableDiagnosticIds.Length > 0;
	}
}