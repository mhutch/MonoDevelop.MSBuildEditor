// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis.Text;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler;

record class MSBuildXmlCompletionContext
    (
        XmlSpineParser SpineParser, XmlCompletionTrigger XmlTriggerKind, ITextSource TextSource, List<XObject> NodePath, int TriggerLineNumber,
        MSBuildResolveResult ResolveResult, MSBuildRootDocument Document, DisplayElementRenderer Renderer, SourceText SourceText
    )
    : XmlCompletionContext
    (
        SpineParser, XmlTriggerKind, TextSource, NodePath, TriggerLineNumber
    )
{
}

class MSBuildXmlCompletionDataSource : XmlCompletionDataSource<MSBuildXmlCompletionContext>
{
    protected override Task<IList<ILspCompletionItem>?> GetElementCompletionsAsync(MSBuildXmlCompletionContext context, bool includeBracket, CancellationToken token)
    {
        var doc = context.Document;

        // we can't use the LanguageElement from the ResolveResult here.
        // if completion is triggered in an existing element's name, the ResolveResult
        // will be for that element, so completion will be for the element's children
        // rather than for the element itself.
        var nodePath = context.NodePath;
        MSBuildElementSyntax? languageElement = null;
        string? elName = null;
        for(int i = 1; i < nodePath.Count; i++)
        {
            if(nodePath[i] is XElement el)
            {
                elName = el.Name.Name;
                if (elName is null)
                {
                    return TaskCompleted(null);
                }
                languageElement = MSBuildElementSyntax.Get(elName, languageElement);
                continue;
            }
            return TaskCompleted(null);
        }

        // if we don't have a language element and we're not at root level, we're in an invalid location
        if(languageElement == null && nodePath.Count > 2)
        {
            return TaskCompleted(null);
        }

        var items = new List<ILspCompletionItem>();

        foreach(var el in doc.GetElementCompletions(languageElement, elName))
        {
            if(el is ItemInfo)
            {
                items.Add(new MSBuildCompletionItem(context, el, XmlCompletionItemKind.SelfClosingElement, includeBracket ? "<" : null));
            } else
            {
                items.Add(new MSBuildCompletionItem(context, el, XmlCompletionItemKind.Element, includeBracket ? "<" : null));
            }
        }

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
                items.Add(new MSBuildCompletionItem(context, att, XmlCompletionItemKind.Attribute));
            }
        }

        return TaskCompleted(items);
    }

    protected override bool AllowTextContentInElement(MSBuildXmlCompletionContext context)
    {
        return base.AllowTextContentInElement(context);
    }
}

class CompletionItemKindExtensions
{
    internal static CompletionItemKind GetCompletionItemKind(ISymbol symbol)
    {
        return CompletionItemKind.Element;
    }
}

class MSBuildCompletionItem(MSBuildXmlCompletionContext context, ISymbol symbol, XmlCompletionItemKind xmlCompletionItemKind, string? prefix = null, string? annotation = null, bool addDescriptionHint = false) : ILspCompletionItem
{
    string label => prefix is not null ? prefix + symbol.Name : symbol.Name;

    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, label, StringComparison.Ordinal);

    public async ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = label };

        if(settings.IncludeDeprecatedPropertyOrTag && symbol.IsDeprecated())
        {
            settings.SetDeprecated(item);
        }

        if (settings.IncludeItemKind)
        {
            item.Kind = CompletionItemKindExtensions.GetCompletionItemKind(symbol);
        }

        if(annotation is not null)
        {
            item.FilterText = $"{symbol.Name} {annotation}";
            item.SortText = annotation;
            if (settings.IncludeLabelDetails)
            {
                item.LabelDetails = new CompletionItemLabelDetails { Description = annotation };
            }
        } else if(addDescriptionHint)
        {
            if (settings.IncludeLabelDetails)
            {
                var descriptionHint = DescriptionFormatter.GetCompletionHint(symbol);
                item.LabelDetails = new CompletionItemLabelDetails { Description = descriptionHint };
            }
        }

        if (settings.IncludeDocumentation)
        {
            var tooltipContent = await context.Renderer.GetInfoTooltipElement(context.SourceText, context.Document, symbol, context.ResolveResult, false, cancellationToken);
            if (tooltipContent is not null)
            {
                item.Documentation = new MarkupContent {
                    //Value = "<code>$(symbol-keyword) <span style='color:#569cd6;'>keyword</span> <span style='color:#9CDCFE;'>Choose</span></code>\r\n\r\nGroups When and Otherwise elements", // tooltipContent,
                    Value = tooltipContent,
                    Kind = MarkupKind.Markdown
                };
            }
        }

        return item;

    }
}