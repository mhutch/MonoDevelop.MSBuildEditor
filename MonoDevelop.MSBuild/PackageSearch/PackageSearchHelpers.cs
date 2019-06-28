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
		public static Task<IReadOnlyList<T>> ToTask<T> (this IPackageFeedSearchJob<T> searchJob, CancellationToken cancelToken = default)
		{
			if (searchJob.RemainingFeeds.Count == 0) {
				return Task.FromResult (searchJob.Results);
			}

			var tcs = new TaskCompletionSource<IReadOnlyList<T>> ();

			//making sure we actually unregister the eventhandler is kinda tricky
			//it could be already completed, or it could complete after we check but before we register
			EventHandler handleSearchUpdated = null;

			cancelToken.Register (() => {
				searchJob.Cancel ();
				if (tcs.TrySetCanceled ()) {
					searchJob.Updated -= handleSearchUpdated;
				}
			});

			handleSearchUpdated = (s, a) => {
				if (!cancelToken.IsCancellationRequested && searchJob.RemainingFeeds.Count == 0) {
					if (tcs.TrySetResult (searchJob.Results)) {
						searchJob.Updated -= handleSearchUpdated;
					}
				}
			};
			searchJob.Updated += handleSearchUpdated;

			if (searchJob.RemainingFeeds.Count == 0) {
				handleSearchUpdated (searchJob, EventArgs.Empty);
			}

			return tcs.Task;
		}
	}
}
