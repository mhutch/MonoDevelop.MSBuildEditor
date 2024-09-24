// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor.Completion;

internal record class MSBuildCompletionTrigger(
    MSBuildResolveResult ResolveResult, ExpressionCompletion.TriggerState TriggerState,
    int SpanStart, int SpanLength,
    ExpressionNode Expression, string ExpressionText,
    ExpressionCompletion.ListKind ListKind, IReadOnlyList<ExpressionNode> ComparandVariables)
{
    public static MSBuildCompletionTrigger? TryCreate(
        XmlSpineParser spine, ITextSource textSource, ExpressionCompletion.ExpressionTriggerReason reason,
        int offset, char typedCharacter, Microsoft.Extensions.Logging.ILogger logger,
        IFunctionTypeProvider functionTypeProvider, MSBuildResolveResult? resolveResult, CancellationToken cancellationToken)
    {
        if(!ExpressionCompletion.IsPossibleExpressionCompletionContext(spine))
        {
            return null;
        }

        // the resolver may modify the spine, so clone it
        // NOTE: this resolver uses an empty root document, so it won't resolve symbols correctly.
        // that's okay, as long as we don't try to use them, and only use the resolved syntax.
        var rr = resolveResult ?? MSBuildResolver.Resolve(spine.Clone(), textSource, MSBuildRootDocument.Empty, functionTypeProvider, logger, cancellationToken);

        if(rr?.ElementSyntax is MSBuildElementSyntax elementSyntax && (rr.Attribute is not null || elementSyntax.ValueKind != MSBuildValueKind.Nothing))
        {
            // TryGetIncompleteValue may return false while still outputting incomplete values, if it fails due to reaching maximum readahead.
            // It will also return false and output null values if we're in an element value that only contains whitespace.
            // In both these cases we can ignore the false return and proceed anyways.
            spine.TryGetIncompleteValue (textSource, out var expressionText, out var valueSpan, cancellationToken: cancellationToken);

            expressionText ??= "";
            int exprStartPos = valueSpan?.Start ?? offset;

            // FIXME: triggering currently depends on errors resulting from the expression ending at the caret
            // so for now we must truncate any readahead that we have obtained
            if (valueSpan.HasValue) {
                int truncateBy = exprStartPos + valueSpan.Value.Length - offset;
                if (truncateBy > 0) {
                    expressionText = expressionText.Substring(0, expressionText.Length - truncateBy);
                }
            }

            var triggerState = ExpressionCompletion.GetTriggerState(
                expressionText,
                offset - exprStartPos,
                reason,
                typedCharacter,
                rr.IsCondition(),
                out int spanStart,
                out int spanLength,
                out ExpressionNode expression,
                out ExpressionCompletion.ListKind listKind,
                out IReadOnlyList<ExpressionNode> comparandVariables,
                logger
            );

            if(triggerState != ExpressionCompletion.TriggerState.None)
            {
                return new(rr, triggerState, exprStartPos + spanStart, spanLength, expression, expressionText, listKind, comparandVariables);
            }
        }
        return null;
    }
}
