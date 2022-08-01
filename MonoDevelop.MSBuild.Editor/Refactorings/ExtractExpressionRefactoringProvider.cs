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

namespace MonoDevelop.MSBuild.Editor.Refactorings
{
	[Export (typeof (MSBuildRefactoringProvider))]
	class ExtractExpressionRefactoringProvider : MSBuildRefactoringProvider
	{
		public override Task RegisterRefactoringsAsync (MSBuildRefactoringContext context)
		{
			if (context.SelectedSpan.Length == 0) {
				return Task.CompletedTask;
			}

			if (GetExpressionExtractionContext (context) is not (ExpressionNode, string, int) exprCtx) {
				return Task.CompletedTask;
			}

			var span = GetValidExtractionSpan (context.SelectedSpan, exprCtx.node);
			if (span is not TextSpan s) {
				return Task.CompletedTask;
			}

			var expression = exprCtx.text.Substring (s.Start - exprCtx.offset, s.Length);

			bool isFirst = true;
			foreach (var pt in GetPropertyInsertionPoints (context.ElementSyntax.SyntaxKind, context.XObject)) {
				context.RegisterRefactoring (new ExtractExpressionAction (expression, s, "MyNewProperty", isFirst? null: pt.scopeName, pt.span, pt.createGroup));
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

		static TextSpan? GetValidExtractionSpan (TextSpan selection, ExpressionNode expr)
		{
			var startNode = expr.Find (selection.Start);
			if (startNode != null) {
				startNode = ExpandToExtractableNode (startNode);
			}
			if (startNode == null) {
				return null;
			}

			var endNode = startNode.End == selection.End ? startNode : expr.Find (selection.End);
			if (endNode != null) {
				endNode = ExpandToExtractableNode (endNode);
			}
			if (endNode == null) {
				return null;
			}

			if (startNode == endNode) {
				if (startNode.WithAllDescendants ().Any (IsForbiddenNode)) {
					return null;
				}
				return TextSpan.FromBounds (
					GetExtractableStart (startNode, selection),
					GetExtractableEnd (startNode, selection)
				);
			}

			if (startNode.Parent == endNode.Parent && startNode.Parent is ConcatExpression) {
				if (startNode.Parent.WithAllDescendants ().Any (IsForbiddenNode)) {
					return null;
				}
				return TextSpan.FromBounds (
					GetExtractableStart (startNode, selection),
					GetExtractableEnd (endNode, selection)
				);
			}

			return null;

			static bool IsForbiddenNode (ExpressionNode n) => n.NodeKind switch
			{
				ExpressionNodeKind.IncompleteExpressionError => true,
				ExpressionNodeKind.Error => true,
				// we need to do more work to figure out when metadata can be extracted
				ExpressionNodeKind.Metadata => true,
				_ => false
			};
		}

		static TextSpan? GetInsertAfterPreviousSiblingElementSpan (XElement el)
			=> el.GetPreviousSiblingElement () is XElement previousElement
				? TextSpan.FromBounds (previousElement.OuterSpan.End, previousElement.NextSibling.Span.Start)
				: null;

		static TextSpan? GetInsertBeforeFirstChildSpan (XElement el)
			=> el.FirstChild is XNode firstChild
				? TextSpan.FromBounds (el.Span.End, firstChild.Span.Start)
				: null;

		static TextSpan? GetInsertAfterLastChildSpan (XElement el)
			=> el.LastChild is XNode lastChild
				? TextSpan.FromBounds (lastChild.Span.End, el.ClosingTag.Span.Start)
				: null;

		internal static IEnumerable<(TextSpan span, string? scopeName, bool createGroup)> GetPropertyInsertionPoints (MSBuildSyntaxKind originKind, XObject originNode)
		{
			// this is the point before which the new property must be inserted
			// if it's inserted after this point, it will be out of order
			// TODO: we should also examine the properties in the extracted expression and make sure we don't put it before any of them
			var beforeElement = originNode.SelfAndParentsOfType<XElement> ().First ();

			switch (originKind) {
			case MSBuildSyntaxKind.Property: {
				var property = beforeElement;
				var propertyGroup = beforeElement.ParentElement;
				var insertSpan = GetInsertAfterPreviousSiblingElementSpan (property) ?? GetInsertBeforeFirstChildSpan (propertyGroup);
				// GetInsertBeforeFirstChildSpan only returns null is it's self closing, and we know it isn't because it has at least one child
				yield return (insertSpan!.Value, null, false);
				break;
			}
			case MSBuildSyntaxKind.Item: {
					var itemGroup = beforeElement.ParentElement;
					beforeElement = itemGroup;
					break;
				}
			case MSBuildSyntaxKind.Metadata: {
					var item = (XElement)beforeElement.Parent;
					var itemGroup = (XElement)item.Parent;
					beforeElement = itemGroup;
					break;
				}
			}

			// walk up the scopes in which propertygroups can exist
			var scope = beforeElement.Parent as XElement;
			while (scope != null) {
				var syntax = MSBuildElementSyntax.Get (scope.Name.FullName);
				switch (syntax?.SyntaxKind) {
				case MSBuildSyntaxKind.Target:
				case MSBuildSyntaxKind.When:
				case MSBuildSyntaxKind.Otherwise:
				case MSBuildSyntaxKind.PropertyGroup:
				case MSBuildSyntaxKind.Project: {
						// find the first non-conditioned propertygroup in the scope before the cutoff
						foreach (var el in scope.Elements) {
							if (el.Span.End > beforeElement.Span.Start) {
								break;
							}
							if (el.NameEquals (MSBuildElementSyntax.PropertyGroup.Name, true) && el.Attributes.Get ("Condition", true) == null && !el.IsSelfClosing && el.IsClosed) {
								// GetInsertAfterLastChildSpan returns null if el.IsSelfClosing but we checked that
								yield return (GetInsertAfterLastChildSpan (el)!.Value, syntax.Name, false);
							}
							break;
						}
					}
					break;
				}

				beforeElement = scope;
				scope = beforeElement.Parent as XElement;
			}
		}

		static int GetExtractableStart (ExpressionNode node, TextSpan sel) => node is ExpressionText t && t.Span.Contains (sel.Start) ? sel.Start : node.Span.Start;
		static int GetExtractableEnd (ExpressionNode node, TextSpan sel) => node is ExpressionText t && t.Span.ContainsOuter (sel.End) ? sel.End : node.Span.End;

		static ExpressionNode? ExpandToExtractableNode (ExpressionNode node)
		{
			switch (node.NodeKind) {
			case ExpressionNodeKind.IncompleteExpressionError:
			case ExpressionNodeKind.Error:
			case ExpressionNodeKind.ConditionOperator:
			case ExpressionNodeKind.ConditionFunction:
			case ExpressionNodeKind.ParenGroup:
				return null;
			case ExpressionNodeKind.ItemFunctionInvocation:
			case ExpressionNodeKind.PropertyFunctionInvocation:
			case ExpressionNodeKind.PropertyRegistryValue:
			case ExpressionNodeKind.ClassReference:
			case ExpressionNodeKind.FunctionName:
			case ExpressionNodeKind.ItemTransform:
			case ExpressionNodeKind.ItemName:
			case ExpressionNodeKind.PropertyName:
			case ExpressionNodeKind.ArgumentList:
				return node.Parent == null ? null : ExpandToExtractableNode (node.Parent);
			case ExpressionNodeKind.ArgumentLiteralBool:
			case ExpressionNodeKind.ArgumentLiteralInt:
			case ExpressionNodeKind.ArgumentLiteralFloat:
			case ExpressionNodeKind.ArgumentLiteralString:
			case ExpressionNodeKind.Item:
			case ExpressionNodeKind.Concat:
			case ExpressionNodeKind.Metadata:
			case ExpressionNodeKind.List:
			case ExpressionNodeKind.Text:
			case ExpressionNodeKind.Property:
			case ExpressionNodeKind.QuotedExpression:
				return node;
			}
			return null;
		}

		class ExtractExpressionAction : SimpleMSBuildCodeAction
		{
			readonly string expr;
			readonly TextSpan sourceSpan;
			readonly string propertyName;
			private readonly string? scopeName;
			readonly TextSpan insertSpan;
			readonly bool createGroup;

			public ExtractExpressionAction (string expr, TextSpan sourceSpan, string propertyName, string? scopeName, TextSpan insertionSpan, bool createGroup)
			{
				this.expr = expr;
				this.sourceSpan = sourceSpan;
				this.propertyName = propertyName;
				this.scopeName = scopeName;
				this.insertSpan = insertionSpan;
				this.createGroup = createGroup;
			}

			public override string Title =>
				scopeName == null
					? $"Extract expression"
					: $"Extract expression to {scopeName} scope";

			EditTextActionOperation.Edit GetNewPropertyWithGroupEdit () => new (
				EditTextActionOperation.EditKind.Replace,
				insertSpan,
				$"\n    <PropertyGroup>\n      <{propertyName}>{expr}</{propertyName}>\n    </PropertyGroup>\n    ",
				new[] {
						new TextSpan (28, propertyName.Length),
						new TextSpan (28 + propertyName.Length + 1 + expr.Length + 2, propertyName.Length)
					}
			);

			EditTextActionOperation.Edit GetNewPropertyEdit () => new (
				EditTextActionOperation.EditKind.Replace,
				insertSpan,
				$"\n    <{propertyName}>{expr}</{propertyName}>\n    ",
				new[] {
						new TextSpan (6, propertyName.Length),
						new TextSpan (6 + propertyName.Length + 1 + expr.Length + 2, propertyName.Length)
					}
			);

			protected override MSBuildCodeActionOperation CreateOperation ()
				=> new EditTextActionOperation ()
				.WithEdit (createGroup
					? GetNewPropertyWithGroupEdit()
					: GetNewPropertyEdit ())
				.Replace (
					sourceSpan.Start, sourceSpan.Length, $"$({propertyName})",
					relativeSelections: new[] {
						new TextSpan (2, propertyName.Length),
					}
				);
		}
	}
}