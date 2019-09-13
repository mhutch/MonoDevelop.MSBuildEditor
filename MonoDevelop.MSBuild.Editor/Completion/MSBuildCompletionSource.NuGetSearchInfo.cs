// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using MonoDevelop.Xml.Editor.Completion;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	partial class MSBuildCompletionSource
	{
		public class NuGetSearchUpdater
		{
			public NuGetSearchUpdater (MSBuildCompletionSource parent, IAsyncCompletionSession session, string tfm)
			{
				this.tfm = tfm;
				this.parent = parent;
				this.session = session;
			}

			string tfm;
			IAsyncCompletionSession session;
			MSBuildCompletionSource parent;
			MSBuildCompletionItemManager completionItemManager;

			// these fields are protected by the locker
			object locker = new object ();
			NuGetSearchJob searchJob;
			ImmutableArray<CompletionItem>? updatedList;
			AsyncCompletionSessionDataSnapshot updatedListData;
			bool isUpdatedListEnqueued;

			public ImmutableArray<CompletionItem> Update (
				MSBuildCompletionItemManager completionItemManager,
				AsyncCompletionSessionDataSnapshot data)
			{
				lock (locker) {
					// kick off an updated search if we need one
					if (searchJob == null || searchJob.IsOutdated (data)) {
						this.completionItemManager = completionItemManager;
						searchJob?.Cancel ();
						searchJob = new NuGetSearchJob (this, data);
					}
					// apply the updated list if there is one
					isUpdatedListEnqueued = false;
					return updatedList ?? data.InitialSortedList;
				}
			}

			void EnqueueUpdate (ImmutableArray<CompletionItem> newList, AsyncCompletionSessionDataSnapshot data, CancellationToken token)
			{
				lock (locker) {
					if (token.IsCancellationRequested) {
						return;
					}
					updatedList = newList;
					updatedListData = data;
					if (isUpdatedListEnqueued) {
						return;
					}
					isUpdatedListEnqueued = true;
				}

				var jtf = parent.provider.JoinableTaskContext.Factory;
				jtf.Run (async delegate {
					await jtf.SwitchToMainThreadAsync ();
					if (!session.IsDismissed) {
						session.OpenOrUpdate (
							new CompletionTrigger (CompletionTriggerReason.Invoke, updatedListData.Snapshot),
							session.ApplicableToSpan.GetStartPoint (updatedListData.Snapshot),
							CancellationToken.None);
					}
				});
			}

			class NuGetSearchJob
			{
				readonly IPackageFeedSearchJob<Tuple<string, FeedKind>> search;
				readonly NuGetSearchUpdater parent;
				readonly CancellationTokenSource cts = new CancellationTokenSource ();
				readonly AsyncCompletionSessionDataSnapshot data;

				public NuGetSearchJob (NuGetSearchUpdater parent, AsyncCompletionSessionDataSnapshot data)
				{
					this.parent = parent;
					this.data = data;

					var filterText = parent.session.ApplicableToSpan.GetText (data.Snapshot);
					search = parent.parent.provider.PackageSearchManager.SearchPackageNames (filterText, parent.tfm);
					search.Updated += SearchUpdated;
					cts.Token.Register (search.Cancel);
				}

				public bool IsOutdated (AsyncCompletionSessionDataSnapshot data)
					=> data.Snapshot.Version.VersionNumber > this.data.Snapshot.Version.VersionNumber;

				public void Cancel () => cts.Cancel ();

				void SearchUpdated (object sender, EventArgs e)
				{
					var token = cts.Token;

					int remainingFeeds = search.RemainingFeeds.Count;
					if (remainingFeeds == 0 || token.IsCancellationRequested) {
						search.Updated -= SearchUpdated;
					}

					if (token.IsCancellationRequested) {
						return;
					}

					var items = new List<CompletionItem> ();
					foreach (var result in search.Results) {
						items.Add (parent.parent.CreateNuGetCompletionItem (result, XmlCompletionItemKind.AttributeValue));
					}

					// if remainingFeeds has changed, an new event has fired, bail out and let it do the work
					if (token.IsCancellationRequested || remainingFeeds != search.RemainingFeeds.Count) {
						return;
					}

					var newList = data.InitialSortedList.AddRange (items);
					parent.completionItemManager.SortCompletionListAsync (
						parent.session,
						new AsyncCompletionSessionInitialDataSnapshot (newList, data.Snapshot, new CompletionTrigger (CompletionTriggerReason.Insertion, data.Snapshot)),
						CancellationToken.None
					);

					if (token.IsCancellationRequested || remainingFeeds != search.RemainingFeeds.Count) {
						return;
					}

					parent.EnqueueUpdate (newList, data, token);
				}
			}
		}
	}
}
