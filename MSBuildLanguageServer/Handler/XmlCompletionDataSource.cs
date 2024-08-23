// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection.Emit;

using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler;

record class XmlCompletionContext(XmlSpineParser SpineParser, XmlCompletionTrigger XmlTriggerKind, ITextSource TextSource, List<XObject> NodePath, int TriggerLineNumber)
{
}

class XmlCompletionDataSource<TContext> where TContext : XmlCompletionContext
{
    public IEnumerable<Task<IList<ILspCompletionItem>?>> GetCompletionTasks(TContext triggerContext, CancellationToken cancellationToken)
    {
        switch(triggerContext.XmlTriggerKind)
        {
        case XmlCompletionTrigger.ElementValue:
            yield return GetElementValueCompletionsAsync(triggerContext, cancellationToken);
            goto case XmlCompletionTrigger.Tag;

        case XmlCompletionTrigger.Tag:
        case XmlCompletionTrigger.ElementName:
            //TODO: if it's on the first or second line and there's no DTD declaration, add the DTDs, or at least <!DOCTYPE
            //TODO: add snippets // MonoDevelop.Ide.CodeTemplates.CodeTemplateService.AddCompletionDataForFileName (DocumentContext.Name, list);
            bool includeBracket = triggerContext.XmlTriggerKind != XmlCompletionTrigger.ElementName;
            yield return GetElementCompletionsAsync(triggerContext, includeBracket, cancellationToken);
            yield return GetMiscellaneousTagsAsync(triggerContext, includeBracket, cancellationToken);
            break;

        case XmlCompletionTrigger.AttributeName:
            if(triggerContext.SpineParser.Spine.TryFind<IAttributedXObject>(maxDepth: 1) is not IAttributedXObject attributedOb)
            {
                throw new InvalidOperationException("Did not find IAttributedXObject in stack for XmlCompletionTrigger.Attribute");
            }
            triggerContext.SpineParser.Clone().AdvanceUntilEnded((XObject)attributedOb, triggerContext.TextSource, 1000);
            var attributes = attributedOb.Attributes.ToDictionary(StringComparer.OrdinalIgnoreCase);
            yield return GetAttributeCompletionsAsync(triggerContext, attributedOb, attributes, cancellationToken);
            break;

        case XmlCompletionTrigger.AttributeValue:
            if(triggerContext.SpineParser.Spine.TryPeek(out XAttribute? att) && triggerContext.SpineParser.Spine.TryPeek(1, out IAttributedXObject? attributedObject))
            {
                yield return GetAttributeValueCompletionsAsync(triggerContext, attributedObject, att, cancellationToken);
            }
            break;

        case XmlCompletionTrigger.Entity:
            bool isElement = triggerContext.NodePath.Count > 0 && triggerContext.NodePath[^1] is XElement;
            if(!isElement || AllowTextContentInElement(triggerContext))
            {
                yield return GetEntityCompletionsAsync(triggerContext, cancellationToken);
            }
            break;

        case XmlCompletionTrigger.DocType:
        case XmlCompletionTrigger.DeclarationOrCDataOrComment:
            yield return GetDeclarationCompletionsAsync(triggerContext, cancellationToken);
            break;
        }
    }

    protected virtual Task<IList<ILspCompletionItem>?> GetElementCompletionsAsync(TContext context, bool includeBracket, CancellationToken token)
        => TaskCompleted(null);

    protected virtual Task<IList<ILspCompletionItem>?> GetElementValueCompletionsAsync(TContext context, CancellationToken token)
        => TaskCompleted(null);

    protected virtual Task<IList<ILspCompletionItem>?> GetAttributeCompletionsAsync(TContext context, IAttributedXObject attributedObject, Dictionary<string, string> existingAttributes, CancellationToken token)
        => TaskCompleted(null);

    protected virtual Task<IList<ILspCompletionItem>?> GetAttributeValueCompletionsAsync(TContext context, IAttributedXObject attributedObject, XAttribute attribute, CancellationToken token)
        => TaskCompleted(null);

    protected virtual Task<IList<ILspCompletionItem>?> GetEntityCompletionsAsync(TContext context, CancellationToken token)
        => TaskCompleted(entityItems);

    protected virtual Task<IList<ILspCompletionItem>?> GetDeclarationCompletionsAsync(TContext context, CancellationToken token)
        => TaskCompleted(
            AllowTextContentInElement(context)
                ? [cdataItemWithBracket, commentItemWithBracket]
                : [commentItemWithBracket]
            );

    protected virtual bool AllowTextContentInElement(TContext context) => true;

    protected static Task<IList<ILspCompletionItem>?> TaskCompleted(IList<ILspCompletionItem>? items) => Task.FromResult(items);

    Task<IList<ILspCompletionItem>?> GetMiscellaneousTagsAsync(TContext triggerContext, bool includeBracket, CancellationToken cancellationToken)
        => Task.Run(() => (IList<ILspCompletionItem>?)GetMiscellaneousTags(triggerContext, includeBracket).ToList(), cancellationToken);

    /// <summary>
    /// Gets completion items for closing tags, comments, CDATA etc.
    /// </summary>
    IEnumerable<ILspCompletionItem> GetMiscellaneousTags(TContext context, bool includeBracket)
    {
        if(context.NodePath.Count == 0 & context.TriggerLineNumber == 0)
        {
            yield return includeBracket ? prologItemWithBracket : prologItem;
        }

        if(AllowTextContentInElement(context))
        {
            yield return includeBracket ? cdataItemWithBracket : cdataItem;
        }

        yield return includeBracket ? commentItemWithBracket : commentItem;

        foreach(var closingTag in GetClosingTags(context.NodePath, includeBracket))
        {
            yield return closingTag;
        }
    }

    IEnumerable<ILspCompletionItem> GetClosingTags(List<XObject> nodePath, bool includeBracket)
    {
        var dedup = new HashSet<string>();

        //FIXME: search forward to see if tag's closed already
        for(int i = nodePath.Count - 1; i >= 0; i--)
        {
            var ob = nodePath[i];
            if(!(ob is XElement el))
                continue;
            if(!el.IsNamed || el.IsClosed)
                yield break;

            string name = el.Name.FullName!;
            if(!dedup.Add(name))
            {
                continue;
            }
            
            yield return new XmlClosingTagCompletionItem (includeBracket, name, el, dedup.Count);
        }
    }

    readonly XmlCompletionItem cdataItem = new ("![CDATA[", XmlToLspCompletionItemKind.CData, "XML character data");
    readonly XmlCompletionItem cdataItemWithBracket = new("<![CDATA[", XmlToLspCompletionItemKind.CData, "XML character data");
    
    readonly XmlCompletionItem commentItem = new("!--", XmlToLspCompletionItemKind.Comment, "XML comment");
    readonly XmlCompletionItem commentItemWithBracket = new("<!--", XmlToLspCompletionItemKind.Comment, "XML comment");

    //TODO: commit $"?xml version=\"1.0\" encoding=\"{encoding}\" ?>"
    readonly XmlCompletionItem prologItem = new("?xml", XmlToLspCompletionItemKind.Prolog, "XML prolog");
    readonly XmlCompletionItem prologItemWithBracket = new("<?xml", XmlToLspCompletionItemKind.Prolog, "XML prolog");

    readonly XmlEntityCompletionItem[] entityItems = [
        new ("apos", "'"),
        new ("quot", "\""),
        new ("lt", "<"),
        new ("gt", ">"),
        new ("amp", "&"),
    ];
}

class XmlCompletionItem(string label, CompletionItemKind kind, string markdownDocumentation) : ILspCompletionItem
{
    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, label, StringComparison.Ordinal);

    public ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = label, Kind = kind };

        if (settings.IncludeDocumentation)
        {
            // TODO: strip markdown if client only supports text
            item.Documentation = markdownDocumentation;
        }

        return new(item);
    }

    static CompletionItem CreateItem(string label, CompletionItemKind kind, string markdownDocumentation)
        => new() { Label = label, Kind = kind, Documentation = CreateMarkdown(markdownDocumentation) };

    static MarkupContent CreateMarkdown(string markdown)
        => new() { Kind = MarkupKind.Markdown, Value = markdown };
}

class XmlEntityCompletionItem(string name, string character) : ILspCompletionItem
{
    readonly string label = $"&{name};";

    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, label, StringComparison.Ordinal);


    //TODO: need to tweak semicolon insertion for XmlCompletionItemKind.Entity
    public ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = label, FilterText = name, Kind = XmlToLspCompletionItemKind.Entity };

        if (settings.IncludeDocumentation)
        {
            item.Documentation = $"Escaped '{character}'";
        };

        return new(item);
    }
}

class XmlClosingTagCompletionItem(bool includeBracket, string name, XElement element, int dedupCount) : ILspCompletionItem
{
    readonly string label = (includeBracket ? "</" : "/") + name;

    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, label, StringComparison.Ordinal);

    // TODO: custom insert text, including for multiple closing tags
    public ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CancellationToken cancellationToken)
    {
        // force these to sort last, they're not very interesting values to browse as these tags are usually already closed
        string sortText = "ZZZZZZ" + label;

        var item = new CompletionItem { Label = label, Kind = XmlToLspCompletionItemKind.ClosingTag, SortText = sortText };

        if(settings.IncludeDocumentation)
        {
            item.Documentation = GetClosingTagDocumentation(element, dedupCount > 1);
        };

        return new(item);
    }


    static MarkupContent GetClosingTagDocumentation(XElement element, bool isMultiple)
        => CreateMarkdown(
            isMultiple
                ? $"Closing tag for element `{element.Name}`, closing all intermediate elements"
                : $"Closing tag for element `{element.Name}`"
            );

    static MarkupContent CreateMarkdown(string markdown) => new() { Kind = MarkupKind.Markdown, Value = markdown };
}

enum XmlCompletionItemKind
{
    Element,
    SelfClosingElement,
    Attribute,
    AttributeValue,
    CData,
    Comment,
    Prolog,
    Entity,
    ClosingTag,
    MultipleClosingTags
}

/// <summary>
/// Central location for mapping XML item kinds to <see cref="CompletionItemKind"/> values
/// </summary>
class XmlToLspCompletionItemKind
{
    public const CompletionItemKind ClosingTag = CompletionItemKind.CloseElement;
    public const CompletionItemKind Comment = CompletionItemKind.TagHelper;
    public const CompletionItemKind CData = CompletionItemKind.TagHelper;
    public const CompletionItemKind Prolog = CompletionItemKind.TagHelper;
    public const CompletionItemKind Entity = CompletionItemKind.TagHelper;
}
