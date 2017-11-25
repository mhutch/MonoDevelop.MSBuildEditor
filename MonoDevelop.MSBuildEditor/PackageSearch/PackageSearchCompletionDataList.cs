// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using MonoDevelop.Ide;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Editor.Extension;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;
using ProjectFileTools.NuGetSearch.Search;
using BF = System.Reflection.BindingFlags;

namespace MonoDevelop.MSBuildEditor.PackageSearch
{
	class PackageNameSearchCompletionDataList : PackageSearchCompletionDataList
	{
		readonly IPackageSearchManager searchManager;
		readonly string tfm;

		public PackageNameSearchCompletionDataList (string initialSearch, IPackageSearchManager searchManager, string tfm)
		{
			this.searchManager = searchManager;
			this.tfm = tfm;
			StartSearch (initialSearch);
		}

		protected override IPackageFeedSearchJob<Tuple<string, FeedKind>> CreateSearch (string partialWord)
		{
			return searchManager.SearchPackageNames (partialWord.ToLower (), tfm);
		}

		protected override bool ShouldUpdate (string newSearch, string oldSearch)
		{
			return newSearch != oldSearch;
		}

		protected override CompletionData CreateCompletionData (string result)
		{
			return new PackageSearchCompletionData (searchManager, result, result, null, tfm);
		}
	}

	class PackageVersionSearchCompletionDataList : PackageSearchCompletionDataList
	{
		readonly IPackageSearchManager searchManager;
		readonly string tfm;
		readonly string packageId;

		public PackageVersionSearchCompletionDataList (IPackageSearchManager searchManager, string tfm, string packageId)
		{
			this.searchManager = searchManager;
			this.tfm = tfm;
			this.packageId = packageId;
			StartSearch ();
		}

		protected override IPackageFeedSearchJob<Tuple<string, FeedKind>> CreateSearch (string partialWord)
		{
			return searchManager.SearchPackageVersions (packageId.ToLower (), tfm);
		}

		protected override bool ShouldUpdate (string newSearch, string oldSearch)
		{
			return false;
		}

		protected override CompletionData CreateCompletionData (string result)
		{
			return new PackageSearchCompletionData (searchManager, result, packageId, result, tfm);
		}
	}

	//HACK: ideally we'd have a custom impl of ICompletionDataList that virtualized through to 
	//a private List<CompletionData> that we could build in a task and swap out, instead
	//of having to perform updates on the main thread.
	//However, the standard completion list has several complex methods (e.g. filtering, sorting)
	//that custom impls have to reimplement from scratch. They should be moved to helper classes.
	abstract class PackageSearchCompletionDataList : CompletionDataList, IMutableCompletionDataList
	{
		string oldSearchString;
		IPackageFeedSearchJob<Tuple<string, FeedKind>> search;

		readonly Type completionWindowType, listWindowType, listWidgetType;
		readonly MethodInfo filterWordsMeth;
		readonly FieldInfo completionWindowWindowField, oldCompletionStringField;
		readonly PropertyInfo listWindowListProp;

		public PackageSearchCompletionDataList ()
		{
			//needed for some hacks
			completionWindowType = typeof (CompletionListWindow);
			listWindowType = completionWindowType.Assembly.GetType ("MonoDevelop.Ide.CodeCompletion.ListWindow");
			listWidgetType = completionWindowType.Assembly.GetType ("MonoDevelop.Ide.CodeCompletion.ListWidget");
			filterWordsMeth = typeof (CompletionListWindow).GetMethod ("FilterWords", BF.NonPublic | BF.Instance);
			completionWindowWindowField = completionWindowType.GetField ("window", BF.NonPublic | BF.Instance);
			listWindowListProp = listWindowType.GetProperty ("List", BF.Public | BF.Instance);
			oldCompletionStringField = listWidgetType.GetField ("oldCompletionString", BF.NonPublic | BF.Instance);

			//HACK: the completion windows crashes if we don't set this. it tries to
			//preserve the selection, so first gets the old selected item - but after
			//we already updated the list
			AutoSelect = false;

			AddKeyHandler (new PackageNameKeyHandler ());
		}

		protected void StartSearch (string initialSearch = "")
		{
			UpdateSearch (initialSearch);
		}

		protected abstract IPackageFeedSearchJob<Tuple<string, FeedKind>> CreateSearch (string partialWord);
		protected abstract bool ShouldUpdate (string newSearch, string oldSearch);
		protected abstract CompletionData CreateCompletionData (string result);

		void UpdateSearch (string searchString)
		{
			if (oldSearchString != null && !ShouldUpdate (searchString, oldSearchString)) {
				return;
			}
			oldSearchString = searchString;

			var newSearch = CreateSearch (searchString);
			if (search != null) {
				search.Cancel ();
				search.Updated -= HandleSearchUpdated;
			}
			search = newSearch;
			search.Updated += HandleSearchUpdated;

			OnChanging (EventArgs.Empty);
			UpdateList ();
		}

		//HACK: when the list changes, completion tries to reselect the old selected
		//item and and crashes if it fails, so instead of clearing and rebuilding we
		//just add the new items into the list
		HashSet<string> itemsInList = new HashSet<string> ();

		void UpdateList ()
		{
			foreach (var result in search.Results) {
				if (itemsInList.Add (result.Item1)) {
					Add (CreateCompletionData (result.Item1));
				}
			}

			//HACK: the completion window wil not re-sort the list
			Sort (Comparer);
			IsSorted = true;

			//HACK: the completion list doesn't refilter after we update it
			//so we have to manually reset the filtering
			if (CompletionWindowManager.Wnd != null) {
				var window = completionWindowWindowField.GetValue (CompletionWindowManager.Wnd);
				var list = listWindowListProp.GetValue (window);
				oldCompletionStringField.SetValue (list, null);
				filterWordsMeth.Invoke (CompletionWindowManager.Wnd, null);
			}

			IsChanging = search.RemainingFeeds.Count > 0;
			OnChanged (EventArgs.Empty);

			//HACK: we shouldn't need to do this - the message should stay if IsChanging is still true
			if (IsChanging) {
				OnChanging (EventArgs.Empty);
			}
		}

		void HandleSearchUpdated (object sender, EventArgs e)
		{
			DispatchService.SynchronizationContext.Post ((_) => {
				UpdateList ();
			}, null);
		}

		public bool IsChanging { get; set; }

		public override CompletionListFilterResult FilterCompletionList (CompletionListFilterInput input)
		{
			//FIXME: there's probably a better place to do this...
			UpdateSearch (input.CompletionString);

			return base.FilterCompletionList (input);
		}

		public event EventHandler Changing;
		public event EventHandler Changed;

		void OnChanging (EventArgs e)
		{
			Changing?.Invoke (this, e);
		}

		void OnChanged (EventArgs e)
		{
			Changed?.Invoke (this, e);
		}

		public void Dispose ()
		{
			search.Cancel ();
			search.Updated -= HandleSearchUpdated;
		}

		//ensure that period and dash do not commit the selection
		class PackageNameKeyHandler : ICompletionKeyHandler
		{
			public bool PostProcessKey (CompletionListWindow listWindow, KeyDescriptor descriptor, out KeyActions keyAction)
			{
				if (descriptor.KeyChar == '.' || descriptor.KeyChar == '-') {
					keyAction = KeyActions.Process;
					return true;
				}
				keyAction = KeyActions.None;
				return false;
			}

			public bool PreProcessKey (CompletionListWindow listWindow, KeyDescriptor descriptor, out KeyActions keyAction)
			{
				return PostProcessKey (listWindow, descriptor, out keyAction);
			}
		}
	}
}