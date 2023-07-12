using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Logging;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	[Export (typeof (IAsyncCompletionItemManagerProvider))]
	[Name (nameof (MSBuildCompletionItemManagerProvider))]
	[ContentType (MSBuildContentType.Name)]
	[TextViewRole (PredefinedTextViewRoles.Editable)]
	[Order (Before = "XmlCompletionItemManagerProvider")]
	[Order (Before = PredefinedCompletionNames.DefaultCompletionItemManager)]
	internal sealed class MSBuildCompletionItemManagerProvider : IAsyncCompletionItemManagerProvider
	{
		IEnumerable<Lazy<IAsyncCompletionItemManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> unorderedCompletionItemManagerProviders;

		IList<Lazy<IAsyncCompletionItemManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> _orderedCompletionItemManagerProviders;
		IList<Lazy<IAsyncCompletionItemManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> OrderedCompletionItemManagerProviders
			=> _orderedCompletionItemManagerProviders ??= Orderer.Order (unorderedCompletionItemManagerProviders);

		public IEditorLoggerFactory LoggerFactory { get; }

		[ImportingConstructor]
		public MSBuildCompletionItemManagerProvider (
			IEditorLoggerFactory loggerFactory,
			[ImportMany] IEnumerable<Lazy<IAsyncCompletionItemManagerProvider, IOrderableContentTypeAndOptionalTextViewRoleMetadata>> unorderedCompletionItemManagerProviders)
		{
			this.LoggerFactory = loggerFactory;
			this.unorderedCompletionItemManagerProviders = unorderedCompletionItemManagerProviders;
		}

		IAsyncCompletionItemManager IAsyncCompletionItemManagerProvider.GetOrCreate (ITextView textView)
			=> LoggerFactory.GetLogger<MSBuildCompletionItemManagerProvider> (textView).InvokeAndLogExceptions (() => GetOrCreate (textView));

		IAsyncCompletionItemManager GetOrCreate (ITextView textView)
			=> textView.Properties.GetOrCreateSingletonProperty (typeof (MSBuildCompletionItemManagerProvider), () => {
				// each content type can only have a single item manager. we don't want to replace the item manager as
				// the existing one provides important functionality. instead, we find the next provider after this one
				// that's valid for MSBuild, which should be the XML one, but should fall back to the default one
				IAsyncCompletionItemManager nextManager = GetNextProvider (textView, textView.TextSnapshot.ContentType, textView.Roles);
				return new MSBuildCompletionItemManager (nextManager, this);
			});

		IAsyncCompletionItemManager GetNextProvider (ITextView textView, IContentType contentType, ITextViewRoleSet roles)
		{
			bool foundThis = false;
			var nextProvider = OrderedCompletionItemManagerProviders
				.Where (p => {
					if (!foundThis) {
						if (p.Metadata.Name == nameof (MSBuildCompletionItemManagerProvider)) {
							foundThis = true;
						}
						return false;
					}
					if (!p.Metadata.ContentTypes.Any (c => contentType.IsOfType (c))) {
						return false;
					}
					if (p.Metadata.TextViewRoles != null && roles != null && !roles.ContainsAny (p.Metadata.TextViewRoles)) {
						return false;
					}
					return true;
				}).First ();
			var nextManager = nextProvider.Value.GetOrCreate (textView);
			return nextManager;
		}
	}

	interface IOrderableContentTypeAndOptionalTextViewRoleMetadata : IContentTypeMetadata, IOrderable
	{
		[DefaultValue (null)]
		IEnumerable<string> TextViewRoles { get; }
	}

	internal sealed class MSBuildCompletionItemManager : IAsyncCompletionItemManager
	{
		readonly MSBuildCompletionItemManagerProvider provider;
		readonly IAsyncCompletionItemManager nextManager;

		public MSBuildCompletionItemManager (IAsyncCompletionItemManager nextManager, MSBuildCompletionItemManagerProvider provider)
		{
			this.nextManager = nextManager;
			this.provider = provider;
		}

		public Task<ImmutableArray<CompletionItem>> SortCompletionListAsync (IAsyncCompletionSession session, AsyncCompletionSessionInitialDataSnapshot data, CancellationToken token)
		{
			return nextManager.SortCompletionListAsync (session, data, token);
		}

		public Task<FilteredCompletionModel> UpdateCompletionListAsync (IAsyncCompletionSession session, AsyncCompletionSessionDataSnapshot data, CancellationToken token)
			=> provider.LoggerFactory.GetLogger<MSBuildCompletionItemManager> (session.TextView).InvokeAndLogExceptions (() => UpdateCompletionListAsyncInternal (session, data, token));

		public Task<FilteredCompletionModel> UpdateCompletionListAsyncInternal (IAsyncCompletionSession session, AsyncCompletionSessionDataSnapshot data, CancellationToken token)
		{
			Task<FilteredCompletionModel> Next () => nextManager.UpdateCompletionListAsync (session, data, token);

			switch (data.Trigger.Reason) {
			case CompletionTriggerReason.Deletion:
			case CompletionTriggerReason.Insertion:
			case CompletionTriggerReason.Backspace:
			case CompletionTriggerReason.Invoke:
				break;
			default:
				return Next ();
			}

			if (!session.Properties.TryGetProperty (typeof (MSBuildCompletionSource.NuGetSearchUpdater), out MSBuildCompletionSource.NuGetSearchUpdater searchInfo)) {
				return Next ();
			}

			//don't pass the CancellationToken to the search job, else filtering operations will cancel searches
			var newList = searchInfo.Update (this, data);

			if (newList.Length != data.InitialSortedList.Length) {
				data = new AsyncCompletionSessionDataSnapshot (
					newList, data.Snapshot, data.Trigger, data.InitialTrigger, data.SelectedFilters, data.IsSoftSelected, data.DisplaySuggestionItem
				);
			}

			return Next ();
		}
	}
}
