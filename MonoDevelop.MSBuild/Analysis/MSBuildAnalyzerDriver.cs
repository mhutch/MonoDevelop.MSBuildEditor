// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Analysis
{
	class MSBuildAnalyzerDriver
	{
		readonly MSBuildAnalysisContextImpl context;

		public MSBuildAnalyzerDriver (List<MSBuildAnalyzer> analyzers)
		{
			context = new MSBuildAnalysisContextImpl ();

			foreach (var analzyer in analyzers) {
				context.RegisterAnalyzer (analzyer);
			}
		}

		public List<MSBuildDiagnostic> Analyze (MSBuildRootDocument doc, CancellationToken token)
		{
			var session = new MSBuildAnalysisSession (context, doc, token);
			var visitor = new MSBuildAnalyzerVisitor (session);

			visitor.Run (doc, token: token);

			return session.Diagnostics;
		}
	}
}
