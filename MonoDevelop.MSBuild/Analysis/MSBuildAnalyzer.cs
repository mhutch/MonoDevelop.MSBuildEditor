// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace MonoDevelop.MSBuild.Analysis
{
	public abstract class MSBuildAnalyzer
	{
		public abstract ImmutableArray<MSBuildDiagnosticDescriptor> SupportedDiagnostics { get; }

		public abstract void Initialize (MSBuildAnalysisContext context);

		internal bool Disabled { get; set;  }
	}
}
