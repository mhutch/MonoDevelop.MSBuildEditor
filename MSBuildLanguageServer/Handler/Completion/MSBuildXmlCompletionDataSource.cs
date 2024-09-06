// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

record class MSBuildXmlCompletionContext
    (
        XmlSpineParser SpineParser, XmlCompletionTrigger XmlTriggerKind, ITextSource TextSource, List<XObject> NodePath, LSP.Range EditRange,
        MSBuildResolveResult ResolveResult, MSBuildRootDocument Document, MSBuildCompletionDocsProvider DocsProvider, SourceText SourceText
    )
    : XmlCompletionContext
    (
        SpineParser, XmlTriggerKind, TextSource, NodePath, EditRange
    )
{
}

class MSBuildXmlCompletionDataSource : XmlCompletionDataSource<MSBuildXmlCompletionContext>
{
    protected override Task<IList<ILspCompletionItem>?> GetElementCompletionsAsync(MSBuildXmlCompletionContext context, bool includeBracket, CancellationToken token)
    {
        var doc = context.Document;

		var nodePath = context.NodePath;
		if (!CompletionHelpers.TryGetElementSyntaxForElementCompletion(nodePath, out MSBuildElementSyntax? languageElement, out string? elementName)) {
			return TaskCompleted(null);
		}

        var items = new List<ILspCompletionItem>();

        foreach(var el in doc.GetElementCompletions(languageElement, elementName))
        {
            if(el is ItemInfo)
            {
                items.Add(new MSBuildCompletionItem(el, XmlCommitKind.SelfClosingElement, context.DocsProvider, includeBracket ? "<" : null));
            } else
            {
                items.Add(new MSBuildCompletionItem(el, XmlCommitKind.Element, context.DocsProvider, includeBracket ? "<" : null));
            }
        }

        bool allowCData = languageElement != null && languageElement.ValueKind != MSBuildValueKind.Nothing;

        return TaskCompleted(items);
    }

    protected override Task<IList<ILspCompletionItem>?> GetAttributeCompletionsAsync(MSBuildXmlCompletionContext context, IAttributedXObject attributedObject, Dictionary<string, string> existingAttributes, CancellationToken token)
    {
        var rr = context.ResolveResult;
        var doc = context.Document;

        if(rr?.ElementSyntax == null)
        {
            return TaskCompleted(null);
        }

        var items = new List<ILspCompletionItem>();

        foreach(var att in rr.GetAttributeCompletions(doc, doc.ToolsVersion))
        {
            if(!existingAttributes.ContainsKey(att.Name))
            {
                items.Add(new MSBuildCompletionItem(att, XmlCommitKind.Attribute, context.DocsProvider));
            }
        }

        return TaskCompleted(items);
    }

    protected override bool AllowTextContentInElement(MSBuildXmlCompletionContext context)
    {
        // when completing a tag name this is used to determine whether to include CDATA
        // so we need to base it off the same MSBuildElementSyntax used for completion
        // TODO: eliminate the duplicate TryGetElementSyntaxForElementCompletion call
        if(context.XmlTriggerKind == XmlCompletionTrigger.ElementName || context.XmlTriggerKind == XmlCompletionTrigger.Tag)
        {
            var nodePath = context.NodePath;
            if(!CompletionHelpers.TryGetElementSyntaxForElementCompletion(nodePath, out MSBuildElementSyntax? languageElement, out _))
            {
                return true;
            }
            return languageElement is null || languageElement.ValueKind != MSBuildValueKind.Nothing;
        }

        // otherwise, it's for entity completion, and the resolveResult is fine
        if(context.ResolveResult?.ElementSyntax is { } elementSyntax)
        {
            return elementSyntax.ValueKind != MSBuildValueKind.Nothing;
        }

        return true;
    }
}
