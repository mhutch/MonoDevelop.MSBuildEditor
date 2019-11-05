// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Editor.Analysis;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
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

			ExpressionNode expr = GetExpressionExtractionContext (context, out string exprStr, out int exprOffset);
			if (expr == null) {
				return Task.CompletedTask;
			}

			var span = GetValidExtractionSpan (context.SelectedSpan, expr);
			if (!(span is TextSpan s)) {
				return Task.CompletedTask;
			}

			exprStr = exprStr.Substring (s.Start - exprOffset, s.Length);
			var insertionPoint = GetPropertyInsertionPoint (context, out bool createGroup);
			if (!createGroup) {
				context.RegisterRefactoring (new ExtractExpressionAction (exprStr, s, "MyNewProperty", insertionPoint, createGroup));
			}

			return Task.CompletedTask;
		}

		static TextSpan GetInsertAfterSpan (XElement el) => TextSpan.FromBounds (el.OuterSpan.End, el.NextSibling.Span.Start);

		static TextSpan GetPropertyInsertionPoint (MSBuildRefactoringContext context, out bool createPropertyGroup)
		{
			var sourceElement = context.XObject.SelfAndParentsOfType<XElement> ().First ();

			// if we're extracting from a property group, just put in in that same propertygroup
			if (context.ElementSyntax.SyntaxKind == MSBuildSyntaxKind.Property) {
				var prev = sourceElement.GetPreviousSiblingElement () ?? sourceElement.ParentElement;
				createPropertyGroup = false;
				return GetInsertAfterSpan (prev);
			}

			// is this scoped to a target or the project?
			XElement insertionScope;
			if (context.ElementSyntax.IsInTarget (sourceElement, out var targetElement)) {
				insertionScope = targetElement;
			} else {
				insertionScope = sourceElement.SelfAndParentsOfType<XElement> ().First (e => e.NameEquals (MSBuildElementSyntax.Project.Name, true));
			}

			// find the first non-conditioned propertygroup in the scope before the source
			XElement candidatePropertyGroup = null;
			foreach (var el in insertionScope.Elements) {
				if (el.Span.End > sourceElement.Span.Start) {
					break;
				}
				if (el.NameEquals (MSBuildElementSyntax.PropertyGroup.Name, true) && el.Attributes.Get ("Condition", false) == null) {
					candidatePropertyGroup = el;
				}
			}

			if (candidatePropertyGroup != null) {
				if (candidatePropertyGroup.IsSelfClosing) {

				}
			}

			createPropertyGroup = true;
			return default (TextSpan);
		}

		static ExpressionNode GetExpressionExtractionContext (MSBuildRefactoringContext context, out string exprText, out int exprOffset)
		{
			if (context.XObject is XText t) {
				if (t.Span.Contains (context.SelectedSpan)) {
					switch (context.ElementSyntax?.SyntaxKind) {
					case MSBuildSyntaxKind.Property:
					case MSBuildSyntaxKind.Metadata:
						exprText = t.Text;
						exprOffset = t.Span.Start;
						return ExpressionParser.Parse (t.Text, ExpressionOptions.ItemsMetadataAndLists, t.Span.Start);
					default:
						break;
					}
				}
				exprText = null;
				exprOffset = 0;
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
						exprText = att.Value;
						exprOffset = att.ValueOffset;
						return ExpressionParser.Parse (att.Value, ExpressionOptions.ItemsMetadataAndLists, att.ValueOffset);
					default:
						if ((context.AttributeSyntax?.SyntaxKind & MSBuildSyntaxKind.ConditionAttribute) != 0) {
							exprText = att.Value;
							exprOffset = att.ValueOffset;
							return ExpressionParser.ParseCondition (att.Value, att.ValueOffset);
						}
						break;
					}
				}
			}

			exprText = null;
			exprOffset = 0;
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
				if (startNode.WithAllDescendants ().OfType<ExpressionError> ().Any ()) {
					return null;
				}
				return TextSpan.FromBounds (
					GetExtractableStart (startNode, selection),
					GetExtractableEnd (startNode, selection)
				);
			}

			if (startNode.Parent == endNode.Parent && startNode.Parent is ConcatExpression) {
				if (startNode.Parent.WithAllDescendants ().OfType<ExpressionError> ().Any ()) {
					return null;
				}
				return TextSpan.FromBounds (
					GetExtractableStart (startNode, selection),
					GetExtractableEnd (endNode, selection)
				);
			}

			return null;
		}

		static int GetExtractableStart (ExpressionNode node, TextSpan sel) => node is ExpressionText t && t.Span.Contains (sel.Start) ? sel.Start : node.Span.Start;
		static int GetExtractableEnd (ExpressionNode node, TextSpan sel) => node is ExpressionText t && t.Span.ContainsOuter (sel.End) ? sel.End : node.Span.End;

		static ExpressionNode ExpandToExtractableNode (ExpressionNode node)
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

		class ExtractExpressionAction : SimpleMSBuildAction
		{
			readonly string expr;
			readonly TextSpan sourceSpan;
			readonly string propertyName;
			readonly TextSpan insertSpan;
			readonly bool createGroup;

			public ExtractExpressionAction (string expr, TextSpan sourceSpan, string propertyName, TextSpan insertionSpan, bool createGroup)
			{
				this.expr = expr;
				this.sourceSpan = sourceSpan;
				this.propertyName = propertyName;
				this.insertSpan = insertionSpan;
				this.createGroup = createGroup;
			}

			public override string Title => $"Extract expression";

			protected override MSBuildActionOperation CreateOperation ()
				=> new EditTextActionOperation ()
				.Replace (insertSpan.Start, insertSpan.Length, $"\n    <{propertyName}>{expr}</{propertyName}>\n    ")
				.Replace (sourceSpan.Start, sourceSpan.Length, $"$({propertyName})");
		}
	}
}