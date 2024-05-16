// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
#nullable enable
#endif

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Dom;

public static class MSBuildDomExtensions
{
	public static bool HasPackageReferenceItems (this MSBuildItemGroupElement itemGroup)
	{
		foreach (var element in itemGroup.Elements) {
			if (element.IsElementNamed ("PackageReference")) {
				return true;
			}
		}
		return false;
	}

	public static bool IsStatic (this MSBuildItemGroupElement itemGroup)
		=> IsStatic ((MSBuildChildElement)itemGroup);

	public static bool IsStatic (this MSBuildPropertyGroupElement propertyGroup)
		=> IsStatic ((MSBuildChildElement)propertyGroup);

	static bool IsStatic (MSBuildChildElement propertyGroupOrItemGroup)
	{
		switch (propertyGroupOrItemGroup.Parent.SyntaxKind) {
		case MSBuildSyntaxKind.When:
		case MSBuildSyntaxKind.Otherwise:
		case MSBuildSyntaxKind.Project:
			return true;
		default:
			return false;
		}
	}
	public static bool? AsConstBool (this MSBuildObject o) => o.Value.AsConstBool ();
	public static string? AsConstString (this MSBuildObject o) => o.Value.AsConstString ();

	public static TextSpan GetValueErrorSpan (this MSBuildObject o) => o.Value?.Span ?? o.NameSpan;
	public static TextSpan GetValueErrorSpan (this MSBuildAttribute att) => att.Value?.Span ?? att.XAttribute.Span;
}
