// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.WpfMarkdown;

class WpfMarkdownStyles
{
	public Style CodeInline { get; set; }
	public Style Emphasis { get; set; }
	public Style EmphasisStrong { get; set; }

	public static WpfMarkdownStyles Default { get; }

	static WpfMarkdownStyles ()
	{
		var codeInline = new Style (typeof (TextElement));
		codeInline.Setters.Add (new Setter (TextElement.FontFamilyProperty, new FontFamily ("Consolas")));

		var emphasis = new Style (typeof (TextElement));
		emphasis.Setters.Add (new Setter (TextElement.FontStyleProperty, FontStyles.Italic));

		var emphasisStrong = new Style (typeof (TextElement));
		emphasisStrong.Setters.Add (new Setter (TextElement.FontWeightProperty, FontWeights.Bold));

		Default = new WpfMarkdownStyles (
			codeInline,
		emphasis,
			emphasisStrong
		);

		Console.WriteLine (VSPackage.Culture);
	}

	WpfMarkdownStyles (Style codeInline, Style emphasis, Style emphasisStrong)
	{
		CodeInline = codeInline;
		Emphasis = emphasis;
		EmphasisStrong = emphasisStrong;
	}
}
