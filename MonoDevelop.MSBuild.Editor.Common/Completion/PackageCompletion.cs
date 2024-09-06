// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

namespace MonoDevelop.MSBuild.Editor.Completion;

static class PackageCompletion
{
	public static bool TryGetPackageVersionSearchJob (
		MSBuildResolveResult? rr,
		MSBuildRootDocument doc,
		IPackageSearchManager packageSearchManager,
		[NotNullWhen(true)] out IPackageFeedSearchJob<Tuple<string, FeedKind>>? packageFeedSearchJob,
		[NotNullWhen (true)] out string? packageId,
		out string? targetFrameworkSearchParameter
		)
	{
		targetFrameworkSearchParameter = null;
		packageId = null;
		packageFeedSearchJob = null;

		if (rr is null || GetItemGroupItemFromMetadata (rr) is not XElement itemEl || GetIncludeOrUpdateAttribute (itemEl) is not XAttribute includeAtt) {
			return false;
		}

		// we can only provide version completions if the item's value type is non-list nugetid
		var itemInfo = doc.GetSchemas ().GetItem (itemEl.Name.Name);
		if (itemInfo == null || !itemInfo.ValueKind.IsKindOrListOfKind (MSBuildValueKind.NuGetID)) {
			return false;
		}

		var packageType = itemInfo.CustomType?.Values[0].Name;

		packageId = includeAtt.Value;
		if (string.IsNullOrEmpty (packageId)) {
			return false;
		}

		// check it's a non-list literal value, we can't handle anything else
		var expr = ExpressionParser.Parse (packageId, ExpressionOptions.ItemsMetadataAndLists);
		if (expr.NodeKind != ExpressionNodeKind.Text) {
			return false;
		}

		targetFrameworkSearchParameter = doc.GetTargetFrameworkNuGetSearchParameter ();

		packageFeedSearchJob = packageSearchManager.SearchPackageVersions (packageId.ToLower (), targetFrameworkSearchParameter, packageType);
		return true;
	}

	static bool ItemIsInItemGroup (XElement itemEl) => itemEl.Parent is XElement parent && parent.Name.Equals (MSBuildElementSyntax.ItemGroup.Name, true);

	static XElement? GetItemGroupItemFromMetadata (MSBuildResolveResult rr)
		=> rr.ElementSyntax.SyntaxKind switch {
			MSBuildSyntaxKind.Item => rr.Element,
			MSBuildSyntaxKind.Metadata => rr.Element.Parent is XElement parentEl && ItemIsInItemGroup (parentEl) ? parentEl : null,
			_ => null
		};

	static XAttribute? GetIncludeOrUpdateAttribute (XElement item)
		=> item.Attributes.FirstOrDefault (att => MSBuildElementSyntax.Item.GetAttribute (att)?.SyntaxKind switch {
			MSBuildSyntaxKind.Item_Include => true,
			MSBuildSyntaxKind.Item_Update => true,
			_ => false
		});
}