// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Drawing;
using System.Windows.Documents;
using System.Windows.Markup;

using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkdigInline = Markdig.Syntax.Inlines.Inline;
using WpfInline = System.Windows.Documents.Inline;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.WpfMarkdown;

/// <summary>
/// Renders markdown as a series of WPF <see cref="WpfInline"/> elements.
/// </summary>
sealed class WpfInlinesMarkdownRenderer : RendererBase
{
	IAddChild currentScope;
	bool isFirstParagraph = true;
	readonly WpfMarkdownStyles markdownStyles;

	public WpfInlinesMarkdownRenderer (IAddChild rootScope, WpfMarkdownStyles? markdownStyles)
	{
		this.markdownStyles = markdownStyles ?? WpfMarkdownStyles.Default;

		ObjectRenderers.Add (new ParagraphRenderer ());
		ObjectRenderers.Add (new LiteralRenderer ());
		ObjectRenderers.Add (new EmphasisInlineRenderer ());
		ObjectRenderers.Add (new LinkInlineRenderer ());
		ObjectRenderers.Add (new CodeInlineRenderer ());

		currentScope = rootScope;
	}

	public override object Render (MarkdownObject markdownObject)
	{
		Write (markdownObject);
		return currentScope;
	}

	/// <summary>
	/// Push WPF text container onto the scope stack, write Markdig inlines into it, then pop it from the stack.
	/// </summary>
	void AddWithChildInlines (IAddChild textContainer, MarkdigInline? firstChild)
	{
		var prevSpanScope = currentScope;
		currentScope = textContainer;

		WriteInlines (firstChild);

		currentScope = prevSpanScope;

		currentScope.AddChild (textContainer);
	}

	/// <summary>
	/// Write a Markdig inline and its siblings into the current scope.
	/// </summary>
	void WriteInlines (MarkdigInline? firstSibling)
	{
		if (firstSibling is null) {
			return;
		}

		MarkdigInline? inline = firstSibling;
		do {
			Write (inline);
			inline = inline.NextSibling;
		} while (inline is not null);
	}

	/// <summary>
	/// Write a Markdig leaf inline into the current scope.
	/// </summary>
	void AddLeafInline (WpfInline leafInline) => currentScope.AddChild (leafInline);

	abstract class WpfObjectRenderer<TObject> : MarkdownObjectRenderer<WpfInlinesMarkdownRenderer, TObject> where TObject : MarkdownObject
	{
	}

	sealed class ParagraphRenderer : WpfObjectRenderer<ParagraphBlock>
	{
		protected override void Write (WpfInlinesMarkdownRenderer renderer, ParagraphBlock paragraph)
		{
			if (renderer.isFirstParagraph) {
				renderer.isFirstParagraph = false;
			} else {
				// fake a paragraph break without using a block element
				// the zero-width space is required for WPF to respect the font size on the empty line
				renderer.AddLeafInline (new Span (new Run ("\n\u200B\n")) { FontSize = SystemFonts.MessageBoxFont.Size * 0.3 });
			}
			renderer.WriteInlines (paragraph.Inline?.FirstChild);
		}
	}

	sealed class LiteralRenderer : WpfObjectRenderer<LiteralInline>
	{
		protected override void Write (WpfInlinesMarkdownRenderer renderer, LiteralInline literalInline)
		{
			renderer.AddLeafInline (new Run (literalInline.Content.ToString ()));
		}
	}

	abstract class FormattedInlineRenderer<TObject,TElement> : WpfObjectRenderer<TObject>
		where TObject : ContainerInline
		where TElement : Span, new ()
	{
		protected sealed override void Write (WpfInlinesMarkdownRenderer renderer, TObject inline)
		{
			// simplify the result when the child is just a literal
			if (inline.HasLiteralChild (out var literalText)) {
				var child = new Run (literalText);
				ApplyFormatting (renderer.markdownStyles, inline, child);
				renderer.AddLeafInline (child);
			} else {
				var child = new TElement ();
				ApplyFormatting (renderer.markdownStyles, inline, child);
				renderer.AddWithChildInlines (child, inline.FirstChild);
			}
		}

		protected abstract void ApplyFormatting (WpfMarkdownStyles styles, TObject inline, TextElement element);
	}

	sealed class EmphasisInlineRenderer : FormattedInlineRenderer<EmphasisInline,Span>
	{
		protected override void ApplyFormatting (WpfMarkdownStyles styles, EmphasisInline emphasisInline, TextElement element)
		{
			if (emphasisInline.DelimiterCount == 1) {
				element.Style = styles.Emphasis;
			} else {
				element.Style = styles.EmphasisStrong;
			}
		}
	}

	sealed class CodeInlineRenderer : WpfObjectRenderer<CodeInline>
	{
		protected sealed override void Write (WpfInlinesMarkdownRenderer renderer, CodeInline codeInline)
		{
			var run = new Run (codeInline.Content.ToString ()) {
				Style = renderer.markdownStyles.CodeInline
			};
			renderer.AddLeafInline (run);
		}
	}

	sealed class LinkInlineRenderer : WpfObjectRenderer<LinkInline>
	{
		protected override void Write (WpfInlinesMarkdownRenderer renderer, LinkInline linkInline)
		{
			renderer.AddWithChildInlines (new Hyperlink { NavigateUri = new Uri (linkInline.Url) }, linkInline.FirstChild);
		}
	}
}
