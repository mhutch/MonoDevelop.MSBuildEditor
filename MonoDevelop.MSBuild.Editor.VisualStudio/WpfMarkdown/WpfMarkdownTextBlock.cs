// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

using Markdig;

using Microsoft.VisualStudio.OLE.Interop;
using static System.Net.Mime.MediaTypeNames;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.WpfMarkdown;

[ContentProperty (nameof(Markdown))]
class WpfMarkdownTextBlock : TextBlock
{
	public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register (
				nameof (Markdown), typeof (string), typeof (WpfMarkdownTextBlock), new PropertyMetadata (OnMarkdownChanged));

	public string? Markdown {
		get => (string?)GetValue (MarkdownProperty);
		set => SetValue (MarkdownProperty, value);
	}

	static void OnMarkdownChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not WpfMarkdownTextBlock markdownTextBlock) {
			return;
		}

		markdownTextBlock.Inlines.Clear ();
		if (e.NewValue is not string markdown) {
			return;
		}

		var pipeline = new MarkdownPipelineBuilder ().Build ();
		var renderer = new WpfInlinesMarkdownRenderer (markdownTextBlock, null);
		Markdig.Markdown.Convert (markdown, renderer, pipeline);
	}
}