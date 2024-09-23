// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace MonoDevelop.MSBuild.Editor.CodeActions
{
	/// <summary>
	/// Convenience base class for a <see cref="MSBuildCodeAction" /> that computes its operations
	/// synchronously using the <see cref="MSBuildDocumentEditBuilder" /> helper
	/// </summary>
	abstract class MSBuildDocumentEditBuilderCodeAction (MSBuildCodeActionContext context) : MSBuildCodeAction
	{
		public MSBuildCodeActionContext Context => context;

		public sealed override Task<MSBuildWorkspaceEdit> ComputeOperationsAsync (CancellationToken cancellationToken)
		{
			return Task.Run (() => {
				var builder = new MSBuildDocumentEditBuilder (
					Context.Document.Filename
					?? throw new System.InvalidOperationException ("Code fix cannot work on document with no name"));
				BuildEdit (builder, cancellationToken);
				return new MSBuildWorkspaceEdit ([ builder.GetDocumentEdit (context.SourceText, context.Options, context.TextFormat) ]);
			}, cancellationToken);
		}

		protected abstract void BuildEdit (MSBuildDocumentEditBuilder builder, CancellationToken cancellationToken);
	}
}