// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

// This is based on Roslyn's Microsoft.CodeAnalysis.Editor.Host.IStreamingFindUsagesPresenter

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace MonoDevelop.MSBuild.Editor.Host
{
	/// <summary>
	/// API for hosts to provide if they can present FindUsages results in a streaming manner.
	/// i.e. if they support showing results as they are found instead of after all of the results
	/// are found.
	/// </summary>
	public interface IStreamingFindReferencesPresenter
	{
		/// <summary>
		/// Tells the presenter that a search is starting.  The returned <see cref="FindReferencesContext"/>
		/// is used to push information about the search into.  i.e. when a reference is found
		/// <see cref="FindReferencesContext.OnReferenceFoundAsync"/> should be called.  When the
		/// search completes <see cref="FindReferencesContext.OnCompletedAsync"/> should be called. 
		/// etc. etc.
		/// </summary>
		/// <param name="title">A title to display to the user in the presentation of the results.</param>
		/// <param name="supportsReferences">Whether or not showing references is supported.
		/// If true, then the presenter can group by definition, showing references underneath.
		/// It can also show messages about no references being found at the end of the search.
		/// If false, the presenter will not group by definitions, and will show the definition
		/// items in isolation.</param>
		FindReferencesContext StartSearch (string title, bool supportsReferences);

		/// <summary>
		/// Clears all the items from the presenter.
		/// </summary>
		void ClearAll ();
	}
	/*
	internal static class IStreamingFindUsagesPresenterExtensions
	{
		/// <summary>
		/// If there's only a single item, navigates to it.  Otherwise, presents all the
		/// items to the user.
		/// </summary>
		public static async Task<bool> TryNavigateToOrPresentItemsAsync (
			this IStreamingFindUsagesPresenter presenter,
			Workspace workspace, string title, ImmutableArray<FoundReference> items)
		{
			if (items.Length == 1 && items[0].SourceSpans.Length <= 1) {
				// There was only one location to navigate to.  Just directly go to that location.
				return items[0].TryNavigateTo (workspace, isPreview: true);
			}

			if (presenter != null) {
				// We have multiple definitions, or we have definitions with multiple locations.
				// Present this to the user so they can decide where they want to go to.
				var context = presenter.StartSearch (title, supportsReferences: false);
				foreach (var definition in nonExternalItems) {
					await context.OnDefinitionFoundAsync (definition).ConfigureAwait (false);
				}

				// Note: we don't need to put this in a finally.  The only time we might not hit
				// this is if cancellation or another error gets thrown.  In the former case,
				// that means that a new search has started.  We don't care about telling the
				// context it has completed.  In the latter case somethign wrong has happened
				// and we don't want to run any more code code in this particular context.
				await context.OnCompletedAsync ().ConfigureAwait (false);
			}

			return true;
		}
	}*/
}