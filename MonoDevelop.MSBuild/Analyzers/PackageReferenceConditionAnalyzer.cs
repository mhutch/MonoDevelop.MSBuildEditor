// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;

namespace MonoDevelop.MSBuild.Analyzers
{
	[MSBuildAnalyzer]
	class PackageReferencePivotLimitationsAnalyzer : MSBuildAnalyzer
	{
		public const string DiagnosticId = nameof (PackageReferencePivotLimitations);

		readonly MSBuildDiagnosticDescriptor PackageReferencePivotLimitations = new MSBuildDiagnosticDescriptor (
			DiagnosticId,
			"PackageReferences should only pivot on TargetFramework",
			"Conditions that affect PackageReferences may lead to unexpected behavior in " +
			"Visual Studio. Pivots on TargetFramework in multi-targeted projects are supported, " +
			"but pivots on Configuration and Configuration-dependent properties will not work " +
			"as expected.",
			MSBuildDiagnosticSeverity.Warning
		);

		public override ImmutableArray<MSBuildDiagnosticDescriptor> SupportedDiagnostics
			=> ImmutableArray.Create (PackageReferencePivotLimitations);

		public override void Initialize (MSBuildAnalysisContext context)
		{
			context.RegisterAttributeAction (AnalyzeItem, MSBuildSyntaxKind.Item_Condition);
			context.RegisterAttributeAction (AnalyzeItemGroup, MSBuildSyntaxKind.ItemGroup_Condition);
			context.RegisterElementAction (AnalyzeChoose, MSBuildSyntaxKind.Choose);
		}

		void AnalyzeItem (AttributeDiagnosticContext ctx)
		{
			var itemGroup = (MSBuildItemGroupElement)ctx.Element.Parent;

			if (itemGroup.IsStatic ()
				&& ctx.Element.IsElementNamed ("PackageReference")
				&& IsConditionInvalidForPackageReferences (ctx.Attribute))
			{
				ctx.ReportDiagnostic (
					new MSBuildDiagnostic (
						PackageReferencePivotLimitations,
						ctx.Attribute.XAttribute.Span
					)
				);
			}
		}

		void AnalyzeItemGroup (AttributeDiagnosticContext ctx)
		{
			var itemGroup = (MSBuildItemGroupElement)ctx.Element;

			if (itemGroup.IsStatic ()
				&& IsConditionInvalidForPackageReferences (ctx.Attribute)
				&& itemGroup.HasPackageReferenceItems ())
			{
				ctx.ReportDiagnostic (
					new MSBuildDiagnostic (
						PackageReferencePivotLimitations,
						ctx.Attribute.XAttribute.Span
					)
				);
			}
		}

		void AnalyzeChoose (ElementDiagnosticContext ctx)
		{
			var chooseElement = (MSBuildChooseElement)ctx.Element;
			bool? hasSubsequentPackageReferences = null;

			foreach (var whenElement in chooseElement.GetElements<MSBuildWhenElement> ()) {
				if (whenElement.ConditionAttribute is MSBuildAttribute att && IsConditionInvalidForPackageReferences (att)) {
					hasSubsequentPackageReferences ??= HasSubsequentPackageReferences (whenElement);
					if (!hasSubsequentPackageReferences.Value) {
						return;
					}
					ctx.ReportDiagnostic (
						new MSBuildDiagnostic (
							PackageReferencePivotLimitations,
							att.XAttribute.Span
						)
					);
				}
			}
		}

		static bool IsConditionInvalidForPackageReferences (MSBuildAttribute condition)
		{
			foreach (var prop in condition.Value.WithAllDescendants ().OfType<ExpressionPropertyName> ()) {
				if (!string.Equals (prop.Name, "TargetFramework", StringComparison.OrdinalIgnoreCase)) {
					return true;
				}
			}
			return false;
		}

		static bool HasSubsequentPackageReferences (MSBuildWhenElement whenElement)
		{
			foreach (var sibling in whenElement.FollowingSiblings) {
				foreach (var itemGroup in sibling.GetElements<MSBuildItemGroupElement> ()) {
					if (itemGroup.HasPackageReferenceItems ())
						return true;
				}
			}
			return false;
		}
	}
}
