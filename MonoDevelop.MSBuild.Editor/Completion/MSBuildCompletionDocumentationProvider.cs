// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.PackageSearch;
using MonoDevelop.Xml.Editor.Completion;

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

namespace MonoDevelop.MSBuild.Editor.Completion;

class MSBuildCompletionDocumentationProvider : ICompletionDocumentationProvider
{
	readonly MSBuildCompletionContext context;
	readonly IPackageSearchManager packageSearchManager;
	readonly DisplayElementFactory displayElementFactory;

	public MSBuildCompletionDocumentationProvider (MSBuildCompletionContext context, IPackageSearchManager packageSearchManager, DisplayElementFactory displayElementFactory)
	{
		this.context = context;
		this.packageSearchManager = packageSearchManager;
		this.displayElementFactory = displayElementFactory;
	}

	public void AttachDocumentation (CompletionItem item, ISymbol symbol)
	{
		item.Properties.AddProperty (typeof (ISymbol), symbol);
		item.AddDocumentationProvider (this);
	}

	public void AttachDocumentation (CompletionItem item, Tuple<string, FeedKind> info)
	{
		item.Properties.AddProperty (typeof (Tuple<string, FeedKind>), info);
		item.AddDocumentationProvider (this);
	}

	public Task<object> GetDocumentationAsync (IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
	{
		// note that the value is a tuple despite the key
		if (item.Properties.TryGetProperty<Tuple<string, FeedKind>> (typeof (Tuple<string, FeedKind>), out var packageSearchResult)) {
			return GetPackageDocumentationAsync (context.Document, packageSearchResult.Item1, packageSearchResult.Item2, token);
		}

		if (item.Properties.TryGetProperty<ISymbol> (typeof (ISymbol), out var info) && info != null) {
			return displayElementFactory.GetInfoTooltipElement (
				session.TextView.TextBuffer, context.Document, info, context.ResolveResult, true, token
			);
		}

		return Task.FromResult<object> (null);
	}

	async Task<object> GetPackageDocumentationAsync (MSBuildRootDocument doc, string packageId, FeedKind feedKind, CancellationToken token)
	{
		var tfm = doc.GetTargetFrameworkNuGetSearchParameter ();
		var packageInfos = await packageSearchManager.SearchPackageInfo (packageId, null, tfm).ToTask (token);
		var packageInfo = packageInfos.FirstOrDefault (p => p.SourceKind == feedKind) ?? packageInfos.FirstOrDefault ();
		if (packageInfo != null) {
			return displayElementFactory.GetPackageInfoTooltip (packageId, packageInfo, feedKind);
		}
		return null;
	}
}
