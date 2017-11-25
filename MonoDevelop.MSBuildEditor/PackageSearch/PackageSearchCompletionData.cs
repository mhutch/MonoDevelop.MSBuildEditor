// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Search;
using System;

namespace MonoDevelop.MSBuildEditor.PackageSearch
{
	class PackageSearchCompletionData : CompletionData
	{
		IPackageSearchManager manager;
		string packageId, packageVersion, tfm;

		public PackageSearchCompletionData (
			IPackageSearchManager manager, string name,
			string packageId, string packageVersion, string tfm)
			: base (name, Stock.Reference)
		{
			this.manager = manager;
			this.packageId = packageId;
			this.packageVersion = packageVersion;
			this.tfm = tfm;
		}

		public override Task<TooltipInformation> CreateTooltipInformation (bool smartWrap, CancellationToken cancelToken)
		{
			var search = manager.SearchPackageInfo (packageId, packageVersion, tfm);

			if (search.RemainingFeeds.Count == 0) {
				return Task.FromResult (CreateInfo (search.Results));
			}

			var tcs = new TaskCompletionSource<TooltipInformation> ();

			//making sure we actually unregister the eventhandler is kinda tricky
			//it could be already completed, or it could complete after we check but before we register
			EventHandler handleSearchUpdated = null;

			cancelToken.Register (() => {
				search.Cancel ();
				if (tcs.TrySetCanceled ()) {
					search.Updated -= handleSearchUpdated;
				}
			});

			handleSearchUpdated = (s, a) => {
				if (!cancelToken.IsCancellationRequested && search.RemainingFeeds.Count == 0) {
					if (tcs.TrySetResult (CreateInfo (search.Results))) {
						search.Updated -= handleSearchUpdated;
					}
				}
			};
			search.Updated += handleSearchUpdated;

			if (search.RemainingFeeds.Count == 0) {
				handleSearchUpdated (search, EventArgs.Empty);
			}

			return tcs.Task;
		}

		TooltipInformation CreateInfo (IReadOnlyList<IPackageInfo> results)
		{
			var result = results.FirstOrDefault ();
			if (result == null) {
				return null;
			}

			return new TooltipInformation {
				SignatureMarkup = $"{result.Id} {result.Version}",
				SummaryMarkup = $"{result.Description}"
			};
		}
	}
}
