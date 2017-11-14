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
using BF = System.Reflection.BindingFlags;

namespace MonoDevelop.MSBuildEditor.PackageSearch
{
	//HACK: ideally we'd have a custom impl of ICompletionDataList that virtualized through to 
	//a private List<CompletionData> that we could build in a task and swap out, instead
	//of having to perform updates on the main thread.
	//However, the standard completion list has several complex methods (e.g. filtering, sorting)
	//that custom impls have to reimplement from scratch. They should be moved to helper classes.
	class PackageSearchCompletionDataList : CompletionDataList, IMutableCompletionDataList
	{
		string searchString;
		IPackageFeedSearchJob<Tuple<string, FeedKind>> search;
		readonly Func<string, IPackageFeedSearchJob<Tuple<string, FeedKind>>> createUpdatedSearch;

		readonly Type completionWindowType, listWindowType, listWidgetType;
		readonly MethodInfo filterWordsMeth;
		readonly FieldInfo completionWindowWindowField, oldCompletionStringField;
		readonly System.Reflection.PropertyInfo listWindowListProp;

		public PackageSearchCompletionDataList (IPackageFeedSearchJob<Tuple<string, FeedKind>> search)
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

			BindSearch (search);
		}

		public PackageSearchCompletionDataList (
			string initialSearchString,
			Func<string, IPackageFeedSearchJob<Tuple<string, FeedKind>>> createSearch
		) : this (createSearch (initialSearchString))
		{
			searchString = initialSearchString;
			createUpdatedSearch = createSearch;
		}

		void BindSearch (IPackageFeedSearchJob<Tuple<string, FeedKind>> newSearch)
		{
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
					Add (result.Item1);
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
			if (createUpdatedSearch != null && input.CompletionString != searchString) {
				searchString = input.CompletionString;
				BindSearch (createUpdatedSearch (searchString));
			}
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