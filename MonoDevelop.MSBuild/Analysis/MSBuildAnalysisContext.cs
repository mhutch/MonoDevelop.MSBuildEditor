// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using MonoDevelop.MSBuild.Language.Syntax;

namespace MonoDevelop.MSBuild.Analysis
{
	public abstract class MSBuildAnalysisContext
	{
		public abstract void RegisterElementAction(Action<ElementDiagnosticContext> action, ImmutableArray<MSBuildSyntaxKind> elementKinds);

		public abstract void RegisterAttributeAction (Action<AttributeDiagnosticContext> action, ImmutableArray<MSBuildSyntaxKind> attributeKinds);

		public abstract void RegisterPropertyWriteAction (Action<PropertyWriteDiagnosticContext> action, ImmutableArray<string> propertyNames);

		public abstract void RegisterCoreDiagnosticFilter (Func<MSBuildDiagnostic, bool> filter, ImmutableArray<string> diagnosticIds);

		public void RegisterElementAction (Action<ElementDiagnosticContext> action, MSBuildSyntaxKind elementKind)
			=> RegisterElementAction (action, ImmutableArray.Create (elementKind));

		public void RegisterAttributeAction (Action<AttributeDiagnosticContext> action, MSBuildSyntaxKind attributeKind)
			=> RegisterAttributeAction (action, ImmutableArray.Create (attributeKind));

		public void RegisterPropertyWriteAction (Action<PropertyWriteDiagnosticContext> action, string propertyName)
			=> RegisterPropertyWriteAction (action, ImmutableArray.Create (propertyName));

		public void RegisterCoreDiagnosticFilter (Func<MSBuildDiagnostic, bool> filter, string diagnosticId)
			=> RegisterCoreDiagnosticFilter (filter, ImmutableArray.Create (diagnosticId));
	}
}