// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Collections.Generic;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Refactorings;

static class MSBuildElementExtensions
{
	/// <summary>
	/// The <c>Condition</c> attribute, if any
	/// </summary>
	public static XAttribute? Condition (this XElement el) => el.Attributes.Get ("Condition", true);

	/// <summary>
	/// Whether the element has a <c>Condition</c> attribute
	/// </summary>
	public static bool HasCondition (this XElement el) => el.Condition () is not null;

	/// <summary>
	/// Child elements that end before the specified offset
	/// </summary>
	public static IEnumerable<XElement> ElementsBefore (this XElement parent, int beforeOffset)
	{
		foreach (var el in parent.Elements) {
			if (el.OuterSpan.End > beforeOffset) {
				yield break;
			}
			yield return el;
		}
	}

	/// <summary>
	/// Child elements of the specified <see cref="MSBuildElementSyntax" />.
	/// </summary>
	public static IEnumerable<XElement> OfSyntax (this IEnumerable<XElement> elements, MSBuildElementSyntax syntax)
	{
		foreach (var el in elements) {
			if (el.Name.Equals (syntax.Name, true)) {
				yield return el;
			}
		}
	}

	/// <summary>
	/// True if a <c>PropertyGroup</c> can exist in this scope
	/// </summary>
	public static bool IsValidPropertyGroupScope (this MSBuildElementSyntax? scope) => scope?.SyntaxKind switch {
		MSBuildSyntaxKind.Target => true,
		MSBuildSyntaxKind.When => true,
		MSBuildSyntaxKind.Otherwise => true,
		MSBuildSyntaxKind.Project => true,
		_ => false
	};
}