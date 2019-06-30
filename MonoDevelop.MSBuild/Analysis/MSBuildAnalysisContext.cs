// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	abstract class MSBuildAnalysisContext
	{
		public abstract void RegisterResolvedElementAction(Action<ResolvedElementDiagnosticContext> action, ImmutableArray<MSBuildSyntaxKind> elementKinds);
		public abstract void RegisterResolvedAttributeAction (Action<ResolvedAttributeDiagnosticContext> action, ImmutableArray<(MSBuildSyntaxKind elementKind, string attributeName)> attributeNames);
		public abstract void RegisterUnknownElementAction (Action<UnknownElementDiagnosticContext> action);
		public abstract void RegisterUnknownAttributeAction (Action<UnknownAttributeDiagnosticContext> action);
		public abstract void RegisterResolvedElementValueAction (Action<ElementValueDiagnosticContext> action, ImmutableArray<MSBuildSyntaxKind> elementKinds);
		public abstract void RegisterResolvedAttributeValueAction (Action<AttributeValueDiagnosticContext> action, ImmutableArray<(MSBuildSyntaxKind elementKind, string attributeName)> attributeNames);
		public abstract void RegisterResolvedTypedValueAction (Action<TypedValueDiagnosticContext> action, ImmutableArray<MSBuildValueKind> valueKind);
		public abstract void RegisterExpressionNodeAction (Action<ExpressionNodeDiagnosticContext> action, ImmutableArray<ExpressionNodeKind> nodeKind);
	}

	struct ResolvedElementDiagnosticContext
	{
		readonly Action<MSBuildDiagnostic> reportDiagnostic;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }
		public MSBuildLanguageElement Element { get; }
		public XElement XElement { get; }

		public ResolvedElementDiagnosticContext (MSBuildLanguageElement element, XElement xelement, MSBuildDocument document, Action<MSBuildDiagnostic> reportDiagnostic, CancellationToken cancellationToken)
		{
			Document = document;
			Element = element;
			XElement = xelement;
			this.reportDiagnostic = reportDiagnostic;
			CancellationToken = cancellationToken;
		}

		public void ReportDiagnostic (MSBuildDiagnostic diagnostic)
		{
			reportDiagnostic (diagnostic);
		}
	}

	struct ResolvedAttributeDiagnosticContext
	{
		readonly Action<MSBuildDiagnostic> reportDiagnostic;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }

		public ResolvedAttributeDiagnosticContext (MSBuildDocument document, Action<MSBuildDiagnostic> reportDiagnostic, CancellationToken cancellationToken)
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

	struct UnknownElementDiagnosticContext
	{
		readonly Action<MSBuildDiagnostic> reportDiagnostic;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }

		public UnknownElementDiagnosticContext (MSBuildDocument document, Action<MSBuildDiagnostic> reportDiagnostic, CancellationToken cancellationToken)
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

	struct UnknownAttributeDiagnosticContext
	{
		readonly Action<MSBuildDiagnostic> reportDiagnostic;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }

		public UnknownAttributeDiagnosticContext (MSBuildDocument document, Action<MSBuildDiagnostic> reportDiagnostic, CancellationToken cancellationToken)
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

	struct ElementValueDiagnosticContext
	{
		readonly Action<MSBuildDiagnostic> reportDiagnostic;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }

		public ElementValueDiagnosticContext (MSBuildDocument document, Action<MSBuildDiagnostic> reportDiagnostic, CancellationToken cancellationToken)
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

	struct AttributeValueDiagnosticContext
	{
		readonly Action<MSBuildDiagnostic> reportDiagnostic;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }

		public AttributeValueDiagnosticContext (MSBuildDocument document, Action<MSBuildDiagnostic> reportDiagnostic, CancellationToken cancellationToken)
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

	struct TypedValueDiagnosticContext
	{
		readonly Action<MSBuildDiagnostic> reportDiagnostic;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }

		public TypedValueDiagnosticContext (MSBuildDocument document, Action<MSBuildDiagnostic> reportDiagnostic, CancellationToken cancellationToken)
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

	struct ExpressionNodeDiagnosticContext
	{
		readonly Action<MSBuildDiagnostic> reportDiagnostic;
		public MSBuildDocument Document { get; }
		public CancellationToken CancellationToken { get; }

		public ExpressionNodeDiagnosticContext (MSBuildDocument document, Action<MSBuildDiagnostic> reportDiagnostic, CancellationToken cancellationToken)
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