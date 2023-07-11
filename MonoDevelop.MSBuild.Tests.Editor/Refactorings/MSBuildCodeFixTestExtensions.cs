// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Text;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Editor.Analysis;

namespace MonoDevelop.MSBuild.Tests.Editor.Refactorings
{
	static class MSBuildCodeFixTestExtensions
	{
		public static async Task<List<MSBuildCodeFix>> GetFixes (this MSBuildEditorTest test, MSBuildCodeFixService codeFixService, ITextBuffer buffer, SnapshotSpan range, MSBuildDiagnosticSeverity requestedSeverities, CancellationToken cancellationToken = default)
		{
			var parser = test.GetParser (buffer);
			var parseResult = await parser.GetOrProcessAsync (buffer.CurrentSnapshot, cancellationToken);

			return await codeFixService.GetFixes (buffer, parseResult, range, requestedSeverities, cancellationToken);
		}
	}
}
