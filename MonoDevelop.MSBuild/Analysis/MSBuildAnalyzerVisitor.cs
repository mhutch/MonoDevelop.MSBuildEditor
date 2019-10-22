// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	class MSBuildAnalyzerVisitor : MSBuildResolvingVisitor
	{
		MSBuildAnalysisSession session;

		public MSBuildAnalyzerVisitor (MSBuildAnalysisSession session)
		{
			this.session = session;
		}

		protected override void VisitResolvedElement (XElement element, MSBuildElementSyntax resolved)
		{
			session.ExecuteElementActions (element, resolved);
			base.VisitResolvedElement (element, resolved);
		}

		protected override void VisitResolvedAttribute (XElement element, XAttribute attribute, MSBuildElementSyntax resolvedElement, MSBuildAttributeSyntax resolvedAttribute)
		{
			session.ExecuteAttributeActions (element, attribute, resolvedElement, resolvedAttribute);
			base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);
		}

		protected override void VisitUnknownElement (XElement element)
		{
			session.ExecuteElementActions (element, null);
			base.VisitUnknownElement (element);
		}

		protected override void VisitUnknownAttribute (XElement element, XAttribute attribute)
		{
			session.ExecuteAttributeActions (element, attribute, null, null);
			base.VisitUnknownAttribute (element, attribute);
		}

		protected override void VisitValueExpression (XElement element, XAttribute attribute, MSBuildElementSyntax resolvedElement, MSBuildAttributeSyntax resolvedAttribute, ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
			if (resolvedElement.SyntaxKind == MSBuildSyntaxKind.Property && resolvedAttribute == null) {
				session.ExecutePropertyWriteActions (element, info, kind, node);
			}

			base.VisitValueExpression (element, attribute, resolvedElement, resolvedAttribute, info, kind, node);
		}
	}
}
