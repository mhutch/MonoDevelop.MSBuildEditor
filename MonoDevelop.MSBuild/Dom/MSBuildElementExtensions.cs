// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Language.Syntax;

namespace MonoDevelop.MSBuild.Analyzers
{
	public static class MSBuildElementExtensions
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
			=> IsStatic ((MSBuildElement)itemGroup);

		public static bool IsStatic (this MSBuildPropertyGroupElement propertyGroup)
			=> IsStatic ((MSBuildElement)propertyGroup);

		static bool IsStatic (MSBuildElement propertyGroupOrItemGroup)
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
	}
}
