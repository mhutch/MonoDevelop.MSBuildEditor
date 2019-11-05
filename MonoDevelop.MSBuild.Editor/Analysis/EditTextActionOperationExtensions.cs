// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	static class EditTextActionOperationExtensions
	{
		public static EditTextActionOperation RenameElement (this EditTextActionOperation op, XElement element, string newName)
			=> op.Replace (element.NameSpan, newName)
				.Replace (element.ClosingTag.Span.Start + 2, element.Name.Length, newName);

		public static EditTextActionOperation RemoveElement (this EditTextActionOperation op, XElement element)
			=> op.DeleteBetween (
				element.FindPreviousNode () switch
				{
					XElement e => e.OuterSpan.End,
					XObject o => o.Span.End,
					_ => element.Span.Start
				},
				element.ClosingTag.Span.End
			);

		public static EditTextActionOperation RemoveAttribute (this EditTextActionOperation op, XAttribute att)
			=> op.DeleteBetween (
				att.FindPreviousSibling ()?.Span.End ?? ((XElement)att.Parent).NameSpan.End,
				att.Span.End
			);
	}
}