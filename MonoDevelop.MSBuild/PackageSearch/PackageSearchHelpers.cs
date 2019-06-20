// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuild.PackageSearch
{
	public static class PackageSearchHelpers
	{
		public static Task<IReadOnlyList<IPackageInfo>> SearchPackageInfo (
			this IPackageSearchManager manager,
			string packageId, string packageVersion, string tfm,
			CancellationToken cancelToken)
		{
			var search = manager.SearchPackageInfo (packageId, packageVersion, tfm);

			if (search.RemainingFeeds.Count == 0) {
				return Task.FromResult (search.Results);
			}

			var tcs = new TaskCompletionSource<IReadOnlyList<IPackageInfo>> ();

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
					if (tcs.TrySetResult (search.Results)) {
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

		/*
		public static TooltipInformation CreateTooltipInformation (IReadOnlyList<IPackageInfo> results)
		{
			var result = results.FirstOrDefault ();
			if (result == null) {
				return null;
			}

			return new TooltipInformation {
				SignatureMarkup = $"{result.Id} {result.Version}",
				SummaryMarkup = $"{result.Description}"
			};
		}*/
	}
}
