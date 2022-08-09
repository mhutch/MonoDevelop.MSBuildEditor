// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Refactorings;

/// <summary>
/// Determines text span for inserting an <see cref="XNode" /> into an existing <see cref="XElement" />.
/// </summary>
static class ElementInsertionSpanExtensions
{
	/// <summary>
	/// Gets text span to insert new node before this <see cref="XElement" />.
	/// </summary>
	/// <returns>The insertion span, or <c>null</c> if element's parent is <c>null</c></returns>
	public static TextSpan? GetInsertBeforeSpan (this XElement el)
		=> el.GetPreviousSiblingElement () is XElement previousElement
			? TextSpan.FromBounds (previousElement.OuterSpan.End, previousElement.NextSibling.Span.Start)
			: el.ParentElement?.GetInsertBeforeFirstChildSpan ();

	/// <summary>
	/// Gets text span to insert new node in this <see cref="XElement" /> before any existing child nodes.
	/// </summary>
	/// <returns>The insertion span, or <c>null</c> if element is self-closing</returns>
	public static TextSpan? GetInsertBeforeFirstChildSpan (this XElement parent)
		=> parent.FirstChild is XNode firstChild
			? TextSpan.FromBounds (parent.Span.End, firstChild.Span.Start)
			: parent.GetInsertInChildlessNodeSpan ();

	/// <summary>
	/// Gets text span to insert new node in this <see cref="XElement" /> after any existing child nodes.
	/// </summary>
	/// <returns>The insertion span, or <c>null</c> if element is self-closing</returns>
	public static TextSpan? GetInsertAfterLastChildSpan (this XElement parent)
		=> parent.LastChild is XNode lastChild
			? TextSpan.FromBounds (lastChild.OuterSpan.End, parent.ClosingTag.Span.Start)
			: parent.GetInsertBeforeFirstChildSpan ();

	/// <summary>
	/// Gets text span to insert new node in this <see cref="XElement" />, assuming it has no existing children.
	/// </summary>
	/// <returns>The insertion span, or <c>null</c> if element is self-closing</returns>
	static TextSpan? GetInsertInChildlessNodeSpan (this XElement el)
		=> el.IsSelfClosing
			? null
			: el.ClosingTag is XNode closing
				? TextSpan.FromBounds (el.Span.End, closing.Span.Start)
				: new TextSpan (el.Span.End, 0);
}