// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Threading.Tasks;

namespace MonoDevelop.MSBuild.Analysis
{
	abstract class MSBuildFixProvider
	{
		public abstract ImmutableArray<string> FixableDiagnosticIds { get; }

		public abstract Task RegisterCodeFixesAsync (MSBuildFixContext context);
	}
}