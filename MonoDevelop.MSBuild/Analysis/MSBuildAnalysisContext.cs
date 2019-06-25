// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Analysis
{
	abstract class MSBuildAnalysisContext
	{
		public abstract void RegisterElementAction(Action<ElementDiagnosticContext> action, ImmutableArray<MSBuildKind> elementKinds);
		public abstract void RegisterItemAction(Action<ItemDiagnosticContext> action, ImmutableArray<string> itemNames);
		public abstract void RegisterPropertyAction(Action<PropertyDiagnosticContext> action, ImmutableArray<string> propertyNames);
	}

	struct ElementDiagnosticContext
	{
		readonly Action<MSBuildDiagnostic> reportDiagnostic;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }
		public MSBuildLanguageElement Element { get; }

		public ElementDiagnosticContext (MSBuildDocument document, MSBuildLanguageElement element, Action<MSBuildDiagnostic> reportDiagnostic, CancellationToken cancellationToken)
		{
			Document = document;
			Element = element;
			this.reportDiagnostic = reportDiagnostic;
			CancellationToken = cancellationToken;
		}

		public void ReportDiagnostic (MSBuildDiagnostic diagnostic)
		{
			reportDiagnostic (diagnostic);
		}
	}

	struct ItemDiagnosticContext
	{
		readonly Action<MSBuildDiagnostic> reportDiagnostic;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }

		public ItemDiagnosticContext(MSBuildDocument document, Action<MSBuildDiagnostic> reportDiagnostic, CancellationToken cancellationToken)
		{
			Document = document;
			this.reportDiagnostic = reportDiagnostic;
			CancellationToken = cancellationToken;
		}

		public void ReportDiagnostic(MSBuildDiagnostic diagnostic)
		{
			reportDiagnostic (diagnostic);
		}
	}

	struct PropertyDiagnosticContext
	{
		readonly Action<MSBuildDiagnostic> reportDiagnostic;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }

		public PropertyDiagnosticContext (MSBuildDocument document, Action<MSBuildDiagnostic> reportDiagnostic, CancellationToken cancellationToken)
		{
			Document = document;
			this.reportDiagnostic = reportDiagnostic;
			CancellationToken = cancellationToken;
		}

		public void ReportDiagnostic (MSBuildDiagnostic diagnostic)
		{
			reportDiagnostic (diagnostic);
		}
	}
}