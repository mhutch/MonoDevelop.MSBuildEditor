// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Analysis
{
	struct MSBuildRefactoringContext
	{
		readonly Action<MSBuildAction> reportRefactoring;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }

		public MSBuildRefactoringContext (MSBuildDocument document, Action<MSBuildAction> reportRefactoring, CancellationToken cancellationToken)
		{
			this.reportRefactoring = reportRefactoring;
			Document = document;
			CancellationToken = cancellationToken;
		}

		public void RegisterRefactoring (MSBuildAction action)
		{
			reportRefactoring (action);
		}
	}
}