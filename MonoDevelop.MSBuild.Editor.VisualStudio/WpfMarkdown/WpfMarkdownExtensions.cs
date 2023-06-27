// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Documents;

using Markdig.Syntax.Inlines;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.WpfMarkdown;

static class WpfMarkdownExtensions
{
	public static bool HasLiteralChild (this ContainerInline container, [NotNullWhen (true)] out string? literalText)
	{
		if (container.FirstChild == container.LastChild && container.FirstChild is LiteralInline literalInline) {
			literalText = literalInline.Content.ToString ();
			return true;
		} else {
			literalText = null;
			return false;
		}
	}
}