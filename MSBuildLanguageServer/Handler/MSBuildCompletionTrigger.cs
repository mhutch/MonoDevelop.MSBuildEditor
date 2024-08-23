// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler;

record class MSBuildCompletionTrigger(MSBuildResolveResult ResolveResult, ExpressionCompletion.TriggerState TriggerState, int SpanStart, int SpanLength, ExpressionNode Expression, string ExpressionText)
{
    public static MSBuildCompletionTrigger? TryCreate(XmlSpineParser spine, ITextSource textSource, ExpressionCompletion.ExpressionTriggerReason reason, int offset, char typedCharacter, Microsoft.Extensions.Logging.ILogger logger, IFunctionTypeProvider functionTypeProvider, CancellationToken cancellationToken)
    {
        if(!ExpressionCompletion.IsPossibleExpressionCompletionContext(spine))
        {
            return null;
        }

        // the resolver may modify the spine, so clone it
        var rr = MSBuildResolver.Resolve(spine.Clone(), textSource, MSBuildRootDocument.Empty, functionTypeProvider, logger, cancellationToken);

        if(rr?.ElementSyntax is MSBuildElementSyntax elementSyntax && (rr.Attribute is not null || elementSyntax.ValueKind != MSBuildValueKind.Nothing))
        {
            string expressionText = spine.GetIncompleteValue(textSource);
            int exprStartPos = offset - expressionText.Length;
            var triggerState = ExpressionCompletion.GetTriggerState(
                expressionText,
                offset - exprStartPos,
                reason,
                typedCharacter,
                rr.IsCondition(),
                out int spanStart,
                out int spanLength,
                out ExpressionNode expression,
                out var _,
                out IReadOnlyList<ExpressionNode> comparandValues,
                logger
            );

            if (triggerState != ExpressionCompletion.TriggerState.None)
            {
                return new(rr, triggerState, spanStart, spanLength, expression, expressionText);
            }
        }
        return null;
    }
}
