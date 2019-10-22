// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Analysis
{
	public abstract class MSBuildAnalysisContext
	{
		public abstract void RegisterElementAction(Action<ElementDiagnosticContext> action, ImmutableArray<MSBuildSyntaxKind> elementKinds);
		public abstract void RegisterAttributeAction (Action<AttributeDiagnosticContext> action, ImmutableArray<MSBuildSyntaxKind> attributeKinds);
	}
}