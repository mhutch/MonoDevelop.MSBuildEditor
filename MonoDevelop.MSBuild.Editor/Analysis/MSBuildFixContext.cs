// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Analysis
{
	struct MSBuildFixContext
	{
		readonly Action<MSBuildAction, ImmutableArray<MSBuildDiagnostic>> reportFix;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }

		public MSBuildFixContext (MSBuildDocument document, Action<MSBuildAction, ImmutableArray<MSBuildDiagnostic>> reportFix, CancellationToken cancellationToken)
		{
			this.reportFix = reportFix;
			Document = document;
			CancellationToken = cancellationToken;
		}

		public void RegisterCodeFix (MSBuildAction action, ImmutableArray<MSBuildDiagnostic> diagnostic)
		{
			reportFix (action, diagnostic);
		}

		public void RegisterCodeFix (MSBuildAction action, MSBuildDiagnostic diagnostic)
		{
			reportFix (action, ImmutableArray.Create (diagnostic));
		}
	}
}