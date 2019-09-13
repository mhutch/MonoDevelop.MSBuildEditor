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
			IAsyncCompletionSession session;

			public NuGetSearchUpdater (MSBuildCompletionSource parent, IAsyncCompletionSession session, string tfm)
			{
				this.tfm = tfm;
				this.parent = parent;
				this.session = session;
			}

			string tfm;
			MSBuildCompletionSource parent;
			ITextSnapshot snapshot;
			IPackageFeedSearchJob<Tuple<string, FeedKind>> search;

			ImmutableArray<CompletionItem> updatedList;
			MSBuildCompletionItemManager itemManager;
			AsyncCompletionSessionDataSnapshot data;

			public ImmutableArray<CompletionItem> Update (
				AsyncCompletionSessionDataSnapshot data, string filterText,
				MSBuildCompletionItemManager itemManager, CancellationToken token)
			{
				lock (this) {
					if (this.data == null || this.data.Snapshot.Version.VersionNumber < data.Snapshot.Version.VersionNumber) {
						if (updatedList == null) {
							updatedList = data.InitialSortedList;
							this.itemManager = itemManager;
						}
						search = parent.provider.PackageSearchManager.SearchPackageNames (filterText, tfm);
						search.Updated += SearchUpdated;
					}
					this.data = data;

					return updatedList;
				}
			}

			void SearchUpdated (object sender, EventArgs e)
			{
				var search = (IPackageFeedSearchJob<Tuple<string, FeedKind>>) sender;
				if (search.RemainingFeeds.Count == 0) {
					search.Updated -= SearchUpdated;
				}

				var items = new List<CompletionItem> ();
				foreach (var result in search.Results) {
					items.Add (parent.CreateNuGetCompletionItem (result, XmlCompletionItemKind.AttributeValue));
				}

				updatedList = updatedList.AddRange (items);

				var jtf = parent.provider.JoinableTaskContext.Factory;
				jtf.Run (async delegate {
					await jtf.SwitchToMainThreadAsync ();
					if (!session.IsDismissed) {
						session.OpenOrUpdate (
							new CompletionTrigger (CompletionTriggerReason.Insertion, data.Snapshot),
							session.ApplicableToSpan.GetStartPoint (data.Snapshot),
							CancellationToken.None);
					}
				});
			}
		}
	}
}
