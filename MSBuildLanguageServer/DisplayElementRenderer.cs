// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable annotations

using System.Text;
using System.Xml;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.PackageSearch;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Logging;

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

using Roslyn.LanguageServer.Protocol;
using Roslyn.Utilities;

using IRoslynSymbol = Microsoft.CodeAnalysis.ISymbol;
using ISymbol = MonoDevelop.MSBuild.Language.ISymbol;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

namespace MonoDevelop.MSBuild.Editor;

// based on MonoDevelop.MSBuild.Editor/DisplayElementFactory.cs
partial class DisplayElementRenderer
{
    readonly StringBuilder sb = new();

    readonly HashSet<string> allowedTags;
    readonly bool supportsMarkdown, supportsIcons;
    readonly ILogger logger;
    bool supportsTagSpan, supportsTagCode;

    public DisplayElementRenderer(ILogger logger, ClientCapabilities clientCapabilities, ClientInfo? clientInfo, MarkupKind[]? contentFormats)
    {
        this.logger = logger;

        allowedTags = clientCapabilities.General?.Markdown?.AllowedTags?.ToHashSet() ?? [];
        supportsTagSpan = allowedTags.Contains("span");
        supportsTagCode = allowedTags.Contains("code");

        supportsMarkdown = contentFormats?.IndexOf(MarkupKind.Markdown) > -1;

        supportsIcons = clientInfo?.Name == "Visual Studio Code";
    }

    void AppendItalic(string text) => sb.Append($"*{text}*");

    public void NewBlock()
    {
        sb.AppendLine();
        if(supportsMarkdown)
        {
            sb.AppendLine();
        }
    }

    public async Task<string?> GetInfoTooltipElement(SourceText buffer, MSBuildRootDocument doc, ISymbol info, MSBuildResolveResult rr, bool hideDeprecationMessage, CancellationToken token)
    {
        sb.Clear();

        if(!WriteNameElement(info))
        {
            return null;
        }

        if(info is IInferredSymbol) {
            NewBlock();
            AppendItalic("(inferred)");
        }

        switch(info.Description.DisplayElement) {
        case IRoslynSymbol symbol:
            if(await GetDocsXml(symbol, token) is string docsXml && !string.IsNullOrEmpty(docsXml))
            {
                try
                {
                    RenderDocsXmlSummaryElement(docsXml);
                } catch(Exception ex)
                {
                    LogDocsRenderingError(logger, ex);
                }
            }
            break;
        case null:
            var descStr = DescriptionFormatter.GetDescription(info, doc, rr);
            if(!string.IsNullOrEmpty(descStr)) {
                NewBlock();
                // already markdown
                // TODO: sanitize
                sb.Append(descStr);
            }
            break;
        default:
            throw new NotSupportedException();
        }

        if(info is VariableInfo vi && !string.IsNullOrEmpty(vi.DefaultValue)) {
            NewBlock();
            sb.Append($"Default value: `{vi.DefaultValue}`");
        }

        /*
        var seenIn = GetSeenInElement(buffer, rr, info, doc);
        if(seenIn != null) {
            elements.Add(seenIn);
        }
        */

        if(!hideDeprecationMessage && info.IsDeprecated ()) {
            AddDeprecationElement(info);
        }

        AddHelpElement(info);

        return sb.ToString();
    }

    void AddDeprecationElement(ISymbol info)
    {
        if(info.IsDeprecated(out string? deprecationMessage)) {
            NewBlock();
            AppendIcon(MSBuildGlyph.Deprecated);

            if(deprecationMessage.StartsWith("Deprecated"))
            {
                sb.Append(deprecationMessage);
            } else
            {
                sb.Append($"Deprecated: {deprecationMessage}");
            }

            EndDiagnosticElement();
        }
    }

    void AddHelpElement(ISymbol info)
    {
        if(supportsMarkdown && info.HasHelpUrl(out string? helpUrl)) {
            NewBlock();
            sb.Append($"[Go to documentation]({helpUrl})");
        }
    }

    enum KnownColor
    {
        Keyword,
        Whitespace,
        Identifier,
        Punctuation,
        Parameter,
        Type,
        Comment
    }

    // FIXME these are hardcoded from VS Code default dark theme
    static string MapColor(KnownColor color) => color switch {
        KnownColor.Keyword => "#569cd6",
        KnownColor.Identifier => "#9CDCFE",
        KnownColor.Punctuation => "#CCCCCC",
        KnownColor.Parameter => "#9CDCFE",
        KnownColor.Type => "#4EC9B0",
        KnownColor.Comment => "#6A9955",
        _ => throw new ArgumentException()
    };

    void BeginVSCodeColorSpan(string vscodeColor) => sb.Append($"<span style='color:var(--vscode-{vscodeColor});'>");
    void BeginHexColorSpan(string color) => sb.Append($"<span style='color:{color};'>");

    void EndSpan()
    {
        sb.Append("</span>");
    }

    void AppendColorSpan(KnownColor color, string text)
    {
        if(supportsMarkdown && supportsTagSpan)
        {
            var mappedColor = MapColor(color);
            BeginHexColorSpan(mappedColor);
            sb.Append(text);
            EndSpan();
        } else
        {
            sb.Append(text);
        }
    }

    void StartSignatureBlock()
    {
        // FIXME: VS Code strips the style attribute from everything except span, and even then it only allows color/background-color
        // so using <code> or <pre> is the only way to get the editor font.
        // HOWEVER, they both have downside. <pre> is a block element, and is styled with top padding, so looks awful.
        // And the hover widget override's <code>'s background color so if we use it for the signature, it doesn't match styling of other hovers.
        if(supportsMarkdown && supportsTagCode)
        {
            //sb.Append("<pre>");
            sb.Append("<code>");
            //sb.Append("<span style='background-color:var(--vscode-editorHoverWidget-background);'>");
        }

        /*
        TODO can we use ```msbuild with a hacky match in the grammar?
        {
            "match": "\u200c\u200c([a-zA-Z-]+) ([a-zA-Z-]+)(?: \: ([a-zA-Z-]+))?\u200c\u200c",
            "captures": {
            "1": { "name": "keyword" },
            "2": { "name": "variable.other" },
            "3": { "name": "entity.name.type" }
            }
        },
        */
    }

    void EndSignatureBlock()
    {
        if(supportsMarkdown && supportsTagCode)
        {
            //sb.Append("</span>");
            sb.Append("</code>");
            //sb.Append("</pre>");
        }
    }

    bool WriteNameElement(ISymbol info)
    {
        var label = DescriptionFormatter.GetTitle(info);
        if(label.kind == null) {
            return false;
        }

        StartSignatureBlock();

        // the icon is a font so put it inside the <code> so size matches signature text
        if (supportsIcons && info.GetGlyph(false) is MSBuildGlyph glyph)
        {
            AppendIcon(glyph);
        }

        AppendColorSpan(KnownColor.Keyword, label.kind);
        sb.Append(' ');
        AppendColorSpan(KnownColor.Identifier, label.name);

        if(info is FunctionInfo fi) {
            if(!fi.IsProperty) {
                AppendColorSpan(KnownColor.Punctuation, "(");

                bool first = true;
                foreach(var p in fi.Parameters) {
                    if(first) {
                        first = false;
                    } else {
                        AppendColorSpan(KnownColor.Punctuation, ",");
                        sb.Append(' ');
                    }

                    AppendColorSpan(KnownColor.Parameter, p.Name);
                    sb.Append(' ');
                    AppendColorSpan(KnownColor.Punctuation, ":");
                    sb.Append(' ');
                    AppendColorSpan(KnownColor.Type, p.Type);
                }
                AppendColorSpan(KnownColor.Punctuation, ")");
            }
        }

        if(info is ITypedSymbol typedSymbol) {
            var tdesc = typedSymbol.GetTypeDescription();
            if(tdesc.Count > 0) {
                var typeInfo = string.Join(" ", tdesc);
                sb.Append(' ');
                AppendColorSpan(KnownColor.Punctuation, ":");
                sb.Append(' ');
                AppendColorSpan(KnownColor.Type, typeInfo);
            }
        }

        EndSignatureBlock();

        return true;
    }

    /*
	ContainerElement GetSeenInElement (ITextBuffer buffer, MSBuildResolveResult rr, ISymbol info, MSBuildRootDocument doc)
	{
		var seenIn = doc.GetDescendedDocumentsReferencingSymbol (info).ToList ();
		if (seenIn.Count == 0) {
			return null;
		}

		Func<string, (string prefix, string remaining)?> shorten = null;

		var elements = new List<ClassifiedTextElement> ();

		int count = 0;
		foreach (var s in seenIn) {
			if (count == 5) {
				elements.Add (new ClassifiedTextElement (
					new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, "["),
					new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, "more in Find References", () => {
						NavigationService.FindReferences (buffer, rr);
					}),
					new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, "]")
				));
				break;
			}
			count++;

			// collapse any .. segments
			string path = System.IO.Path.GetFullPath (s);

			//factor out some common prefixes into variables
			//we do this instead of using the original string, as the result is simpler
			//and easier to understand
			shorten ??= CreateFilenameShortener (doc.Environment);
			var replacement = shorten (path);
			if (!replacement.HasValue) {
				elements.Add (
					new ClassifiedTextElement (
						new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, path, () => OpenFile (path), path)
					)
				);
				continue;
			}

			elements.Add (new ClassifiedTextElement (
				new ClassifiedTextRun (PredefinedClassificationTypeNames.SymbolReference, replacement.Value.prefix),
				new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, replacement.Value.remaining, () => OpenFile (path), path)
			));
		}

		if (elements.Count == 0) {
			return null;
		}

		elements.Insert (0, new ClassifiedTextElement (new ClassifiedTextRun (PredefinedClassificationTypeNames.Other, "Seen in:")));
		return new ContainerElement (ContainerElementStyle.Stacked, elements);
	}
    */

        void AddBreak() => sb.AppendLine("<br/>");

    public string GetResolvedPathElement(List<NavigationAnnotation> navs)
    {
        sb.Clear();

        if(navs.Count == 1) {
            sb.Append("Resolved path: ");
            AddFileLink(navs[0].Path);
            return sb.ToString();
        }

        sb.Append("Resolved paths:");

        int i = 0;
        foreach(var location in navs) {
            AddBreak();
            AddFileLink(location.Path);
            if(i == 5) {
                AddBreak();
                // TODO: make this a link
                sb.Append("[More in Go to Definition]");
                break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Shortens filenames by extracting common prefixes into MSBuild properties. Returns null if the name could not be shortened in this way.
    /// </summary>
    public Func<string, (string prefix, string remaining)?> CreateFilenameShortener(IMSBuildEnvironment environment)
    {
        var prefixes = GetPrefixes(environment);
        return s => GetLongestReplacement(s, prefixes);
    }

    static List<(string prefix, string subst)> GetPrefixes(IMSBuildEnvironment environment)
    {
        var list = new List<(string prefix, string subst)>();
        if(environment.ToolsPath is string toolsPath) {
            list.Add((toolsPath, $"$({ReservedPropertyNames.binPath})"));
        }

        if(environment.ToolsetProperties != null) {
            var wellKnownPathProperties = new[] { WellKnownProperties.MSBuildSDKsPath, WellKnownProperties.MSBuildExtensionsPath, WellKnownProperties.MSBuildExtensionsPath32, WellKnownProperties.MSBuildExtensionsPath64 };
            foreach(var propName in wellKnownPathProperties) {
                if(environment.ToolsetProperties.TryGetValue(propName, out var propVal)) {
                    list.Add((propVal, $"$({propName})"));
                }
            }
        }

        return list;
    }

    static (string prefix, string remaining)? GetLongestReplacement(string val, List<(string prefix, string subst)> replacements)
    {
        (string prefix, string subst)? longestReplacement = null;
        foreach(var replacement in replacements) {
            if(val.StartsWith(replacement.prefix, StringComparison.OrdinalIgnoreCase)) {
                if(!longestReplacement.HasValue || longestReplacement.Value.prefix.Length < replacement.prefix.Length) {
                    longestReplacement = replacement;
                }
            }
        }

        if(longestReplacement.HasValue) {
            return (longestReplacement.Value.subst, val.Substring(longestReplacement.Value.prefix.Length));
        }

        return null;
    }

    Task<string?> GetDocsXml(IRoslynSymbol symbol, CancellationToken token)
    {
        return Task.Run(() => {
            try {
                // MSBuild uses property getters directly but they don't typically have docs.
                // Use the docs from the property instead.
                // FIXME: this doesn't seem to work for the indexer string[]get_Chars, at least on Mono
                if(symbol is IMethodSymbol method && method.MethodKind == MethodKind.PropertyGet) {
                    symbol = method.AssociatedSymbol ?? symbol;
                }
                return symbol.GetDocumentationCommentXml(expandIncludes: true, cancellationToken: token);
            } catch(Exception ex) when(!(ex is OperationCanceledException && token.IsCancellationRequested)) {
                LogDocsLoadingError(logger, ex);
            }
            return null;
        }, token);
    }

    // roslyn's IDocumentationCommentFormattingService seems to be basically unusable
    // without internals access, so do some basic formatting ourselves
    bool RenderDocsXmlSummaryElement(string docs)
    {
        var docsXml = XDocument.Parse(docs);
        var summaryEl = docsXml.Root?.Element("summary");
        if(summaryEl == null) {
            return false;
        }

        AddBreak();

        foreach(var node in summaryEl.Nodes()) {
            switch(node) {
            case XText text:
                // TODO: escaping
                sb.Append(text.Value);
                break;
            case XElement el:
                switch(el.Name.LocalName) {
                case "see":
                    AddTypeNameFromCref(el, logger);
                    continue;
                case "attribution":
                    continue;
                case "para":
                    NewBlock();
                    RenderXmlDocsPara(el, logger);
                    continue;
                default:
                    LogDocsUnexpectedElement(logger, "summary", el.Name.ToString());
                    continue;
                }
            default:
                LogDocsUnexpectedNode(logger, "summary", node.NodeType);
                continue;
            }
        }

        return true;
    }

    void AddTypeNameFromCref(XElement el, ILogger logger)
    {
        if(el.Attribute("cref") is { } att && att.Value is string cref) {
            var colonIdx = cref.IndexOf(':');
            if(colonIdx > -1) {
                cref = cref.Substring(colonIdx + 1);
            }
            if(!string.IsNullOrEmpty(cref)) {
                AppendColorSpan(KnownColor.Type, cref);
            }
        } else {
            LogDocsMissingAttribute(logger, "see", "cref");
        }
    }

    void RenderXmlDocsPara(XElement para, ILogger logger)
    {
        foreach(var node in para.Nodes()) {
            switch(node) {
            case XText text:
                sb.Append(text.Value);
                continue;
            case XElement el:
                switch(el.Name.LocalName) {
                case "see":
                    AddTypeNameFromCref(el, logger);
                    continue;
                default:
                    LogDocsUnexpectedElement(logger, "para", el.Name.ToString());
                    continue;
                }
            default:
                LogDocsUnexpectedNode(logger, "para", node.NodeType);
                continue;
            }
        }
    }

    // TODO: we should be able to do progress reporting here
    public string GetPackageInfoTooltip(string packageId, IPackageInfo package, FeedKind feedKind)
    {
        sb.Clear();

        StartSignatureBlock();

        // TODO: GetImageElement (feedKind),
        AppendColorSpan(KnownColor.Keyword, "package");
        sb.Append(" ");
        AppendColorSpan(KnownColor.Type, package?.Id ?? packageId);

        EndSignatureBlock();

        NewBlock();

        if(package is null)
        {
            AppendColorSpan(KnownColor.Comment, "Could not load package information");
            return sb.ToString();
        }

        var description = !string.IsNullOrWhiteSpace(package.Description) ? package.Description : package.Summary;
        if(string.IsNullOrWhiteSpace(description)) {
            description = package.Summary;
        }
        if(!string.IsNullOrWhiteSpace(description)) {
            // TODO: VS Code sanitizes this but we should too
            sb.Append(description);
        } else {
            AppendColorSpan(KnownColor.Comment, "[no description]");
        }

        if(!supportsMarkdown)
        {
            return sb.ToString();
        }

        var nugetOrgUrl = package.GetNuGetOrgUrl();
        if(nugetOrgUrl != null) {
            NewBlock();
            AddLink(nugetOrgUrl, "Go to package on NuGet.org");
        }

        var projectUrl = package.ProjectUrl != null && Uri.TryCreate(package.ProjectUrl, UriKind.Absolute, out var parsedUrl) && parsedUrl.Scheme == Uri.UriSchemeHttps
            ? package.ProjectUrl : null;
        if(projectUrl != null) {
            NewBlock();
            AddLink(projectUrl, "Go to project URL");
        }

        return sb.ToString();
    }

    void AddLink(string url, string linkText)
    {
        if(!supportsMarkdown)
        {
            throw new NotSupportedException("Cannot add link when markdown is not supported");
        }
        sb.Append($"[{Escape(linkText)}]({url})");
    }

    void AddFileLink(string filePath, string? linkText = null)
    {
        if(!supportsMarkdown)
        {
            sb.Append(filePath);
            return;
        }
        var fullPath = Path.GetFullPath(filePath);
        var uriString = ProtocolConversions.GetAbsoluteUriString(fullPath);
        sb.Append($"[{Escape(linkText ?? filePath)}]({uriString})");
    }

    static string Escape(string s) => ProtocolConversions.EscapeMarkdown(s);

    //public object GetDiagnosticTooltip (MSBuildDiagnostic diagnostic) => GetDiagnosticElement (diagnostic.Descriptor.Severity, diagnostic.GetFormattedMessage () ?? diagnostic.GetFormattedTitle ());

    void AppendIcon(MSBuildGlyph glyph)
    {
        if(!supportsIcons)
        {
            return;
        }
        var iconName = glyph.ToVSCodeImage().ToVSCodeImageId();
        sb.Append($"$({iconName}) "); //  icons are text so add a trailing space to separate from following text
    }

    void StartDiagnosticElement(MSBuildDiagnosticSeverity severity)
    {
        AppendIcon(severity.ToGlyph());

        /*
		var imageId = severity switch {
			MSBuildDiagnosticSeverity.Error => KnownImages.StatusError,
			MSBuildDiagnosticSeverity.Warning => KnownImages.StatusWarning,
			_ => KnownImages.StatusInformation
		};
        */

        // should we show the title as well as the description? it's not possible to align the image cleanly if we do that
        //var titleElement = new ClassifiedTextElement (new ClassifiedTextRun (PredefinedClassificationTypeNames.NaturalLanguage, diagnostic.GetFormattedTitle (), ClassifiedTextRunStyle.Bold));

        /*
		var messageElements = FormatDescriptionText (message);
		var imageElement = GetImageElement (imageId);

		return new ContainerElement (
			ContainerElementStyle.Wrapped | ContainerElementStyle.VerticalPadding,
			imageElement,
			new ClassifiedTextElement (messageElements)
		);
        */
    }

    void EndDiagnosticElement() { }

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "XML docs element '{elementName}' has unexpected element '{childElementName}'")]
    static partial void LogDocsUnexpectedElement(ILogger logger, string elementName, UserIdentifiable<string> childElementName);

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "XML docs element '{elementName}' has unexpected attribute '{attributeName}'")]
    static partial void LogDocsUnexpectedAttribute(ILogger logger, string elementName, UserIdentifiable<string> attributeName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "XML docs element '{elementName}' has unexpected attribute '{attributeName}'")]
    static partial void LogDocsMissingAttribute(ILogger logger, string elementName, UserIdentifiable<string> attributeName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "XML docs element '{elementName}' has unexpected node of type {nodeType}")]
    static partial void LogDocsUnexpectedNode(ILogger logger, string elementName, XmlNodeType nodeType);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Error loading XML docs")]
    static partial void LogDocsLoadingError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Error rendering XML docs")]
    static partial void LogDocsRenderingError(ILogger logger, Exception ex);
}
