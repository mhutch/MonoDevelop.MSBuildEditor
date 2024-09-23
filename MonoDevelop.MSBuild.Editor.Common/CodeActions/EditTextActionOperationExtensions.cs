// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.CodeActions;

static class EditTextActionOperationExtensions
{
	public static MSBuildDocumentEditBuilder RenameElement (this MSBuildDocumentEditBuilder builder, XElement element, string newName)
	{
		builder.Replace (element.NameSpan, newName);
		if (element.ClosingTag is XClosingTag closingTag) {
			builder.Replace (closingTag.NameSpan, newName);
		}
		return builder;
	}

	public static MSBuildDocumentEditBuilder RemoveElement (this MSBuildDocumentEditBuilder builder, XElement element)
	{
		var start = element.FindPreviousNode () switch {
			XElement e => e.OuterSpan.End,
			XObject o => o.Span.End,
			_ => element.Span.Start
		};

		var end = element.ClosingTag?.Span.End ?? element.Span.End;

		return builder.DeleteBetween (start, end);
	}

	public static MSBuildDocumentEditBuilder RemoveAttribute (this MSBuildDocumentEditBuilder builder, XAttribute att)
	{
		int start = att.FindPreviousSibling ()?.Span.End ?? (att.Parent as XElement)?.NameSpan.End ?? att.Span.Start;
		int end = att.Span.End;
		return builder.DeleteBetween (start, end);
	}
}