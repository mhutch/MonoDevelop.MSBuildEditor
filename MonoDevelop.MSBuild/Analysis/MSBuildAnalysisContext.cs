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

		public abstract void RegisterPropertyWriteAction (Action<PropertyWriteDiagnosticContext> action, ImmutableArray<string> propertyNames);

		public void RegisterElementAction (Action<ElementDiagnosticContext> action, MSBuildSyntaxKind attributeKind)
			=> RegisterElementAction (action, ImmutableArray.Create (attributeKind));

		public void RegisterAttributeAction (Action<AttributeDiagnosticContext> action, MSBuildSyntaxKind attributeKind)
			=> RegisterAttributeAction (action, ImmutableArray.Create (attributeKind));

		public void RegisterPropertyWriteAction (Action<PropertyWriteDiagnosticContext> action, string propertyName)
			=> RegisterPropertyWriteAction (action, ImmutableArray.Create (propertyName));
	}
}