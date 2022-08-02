// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Editor.Analysis;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Refactorings.ExtractExpression;

[Export (typeof (MSBuildRefactoringProvider))]
partial class ExtractExpressionRefactoringProvider : MSBuildRefactoringProvider
{
	public override Task RegisterRefactoringsAsync (MSBuildRefactoringContext context)
	{
		if (context.SelectedSpan.Length == 0) {
			return Task.CompletedTask;
		}

		if (GetExpressionExtractionContext (context) is not (ExpressionNode, string, int) exprCtx) {
			return Task.CompletedTask;
		}

		var span = ExpressionNodeExtraction.GetValidExtractionSpan (context.SelectedSpan, exprCtx.node);
		if (span is not TextSpan s) {
			return Task.CompletedTask;
		}

		var expression = exprCtx.text.Substring (s.Start - exprCtx.offset, s.Length);

		bool isFirst = true;
		foreach (var pt in GetPropertyInsertionPoints (context.ElementSyntax.SyntaxKind, context.XObject)) {
			context.RegisterRefactoring (new ExtractExpressionAction (expression, s, "MyNewProperty", isFirst ? null : pt.scopeName, pt.span, pt.createGroup));
			isFirst = false;
		}

		return Task.CompletedTask;
	}

	static (ExpressionNode node, string text, int offset)? GetExpressionExtractionContext (MSBuildRefactoringContext context)
	{
		if (context.XObject is XText t) {
			if (t.Span.Contains (context.SelectedSpan)) {
				switch (context.ElementSyntax?.SyntaxKind) {
				case MSBuildSyntaxKind.Property:
				case MSBuildSyntaxKind.Metadata:
					return (
						ExpressionParser.Parse (t.Text, ExpressionOptions.ItemsMetadataAndLists, t.Span.Start),
						t.Text,
						t.Span.Start);
				default:
					break;
				}
			}
			return null;
		}

		if (context.XObject is XAttribute att) {
			if (att.Span.Contains (context.SelectedSpan)) {
				switch (context.AttributeSyntax?.SyntaxKind) {
				case MSBuildSyntaxKind.Item_Metadata:
				case MSBuildSyntaxKind.Item_Include:
				case MSBuildSyntaxKind.Item_Exclude:
				case MSBuildSyntaxKind.Item_Update:
				case MSBuildSyntaxKind.Item_Remove:
				case MSBuildSyntaxKind.Task_Parameter:
				case MSBuildSyntaxKind.Target_AfterTargets:
				case MSBuildSyntaxKind.Target_BeforeTargets:
				case MSBuildSyntaxKind.Target_DependsOnTargets:
				case MSBuildSyntaxKind.Target_Inputs:
					return (
						ExpressionParser.Parse (att.Value, ExpressionOptions.ItemsMetadataAndLists, att.ValueOffset),
						att.Value,
						att.ValueOffset);
				default:
					if ((context.AttributeSyntax?.SyntaxKind & MSBuildSyntaxKind.ConditionAttribute) != 0) {
						return (
							ExpressionParser.ParseCondition (att.Value, att.ValueOffset),
							att.Value,
							att.ValueOffset);
					}
					break;
				}
			}
		}

		return null;
	}

	/// <summary>
	/// Determine the points to which a property can be extracted.
	/// </summary>
	/// <param name="originElementKind">The <see cref="MSBuildSyntaxKind"/> of the element from which the expression is being extracted</param>
	/// <param name="originNode">The <see cref="XNode"/> of the element or attribute from which the expression is being extracted </param>
	/// <returns></returns>
	internal static IEnumerable<(TextSpan span, string scopeName, bool createGroup)> GetPropertyInsertionPoints (MSBuildSyntaxKind originElementKind, XObject originNode)
	{
		// this is the point before which the new property must be inserted
		// if it's inserted after this point, it will be out of order
		// TODO: we should also examine the properties in the extracted expression and make sure we don't put it before any of them
		var beforeElement = originNode.SelfAndParentsOfType<XElement> ().First ();

		// if we're in a property, first insert it into the same propertyGroup
		if (originElementKind == MSBuildSyntaxKind.Property) {
			// GetInsertBeforeSpan only returns null if the parent is null and we know it's not
			TextSpan insertSpan = beforeElement.GetInsertBeforeSpan ()!.Value;
			yield return (insertSpan, MSBuildElementSyntax.PropertyGroup.Name, false);

			// next insertion point is before the propertygroup's parent
			beforeElement = beforeElement.ParentElement?.ParentElement;
		}

		// walk up the scopes in which propertygroups can exist
		while (beforeElement?.ParentElement is XElement scope) {
			var syntax = MSBuildElementSyntax.Get (scope.Name.FullName);
			if (syntax.IsValidPropertyGroupScope ()) {
				var elementsBeforeOffset = scope.ElementsBefore (beforeElement.Span.Start);
				if (elementsBeforeOffset.OfSyntax (MSBuildElementSyntax.PropertyGroup).LastOrDefault (n => !n.HasCondition () && !n.IsSelfClosing && n.IsClosed) is XElement existingPg) {
					// GetInsertAfterLastChildSpan only returns null if it's self-closing and we already checked it's not
					var insertionSpan = existingPg.GetInsertAfterLastChildSpan ()!.Value;
					yield return (insertionSpan, syntax.Name, false);
				} else {
					// insert a PropertyGroup
					// GetInsertBeforeSpan only returns null if the parent is null and we know it's not
					var insertionSpan = beforeElement.GetInsertBeforeSpan ()!.Value;
					yield return (insertionSpan, syntax.Name, true);
				}
			}
			beforeElement = scope;
		}
	}
}