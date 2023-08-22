using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.PatternMatching;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Logging;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	[Export (typeof (IAsyncCompletionItemManagerProvider))]
	[Name (nameof (MSBuildCompletionItemManagerProvider))]
	[ContentType (MSBuildContentType.Name)]
	[TextViewRole (PredefinedTextViewRoles.Editable)]
	[Order (Before = XmlCompletionItemManager.ProviderName)]
	[Order (Before = PredefinedCompletionNames.DefaultCompletionItemManager)]
	internal sealed class MSBuildCompletionItemManagerProvider : IAsyncCompletionItemManagerProvider
	{
		readonly IEditorLoggerFactory loggerFactory;
		readonly IPatternMatcherFactory patternMatcherFactory;

		[ImportingConstructor]
		public MSBuildCompletionItemManagerProvider (
			IEditorLoggerFactory loggerFactory,
			IPatternMatcherFactory patternMatcherFactory)
		{
			this.loggerFactory = loggerFactory;
			this.patternMatcherFactory = patternMatcherFactory;
		}

		public IAsyncCompletionItemManager GetOrCreate (ITextView textView)
		{
			return textView.Properties.GetOrCreateSingletonProperty (() =>
				new MSBuildCompletionItemManager (patternMatcherFactory, loggerFactory.CreateLogger<MSBuildCompletionItemManager> (textView))
			);
		}
	}

	internal sealed class MSBuildCompletionItemManager : XmlCompletionItemManager
	{
		public MSBuildCompletionItemManager (IPatternMatcherFactory patternMatcherFactory, ILogger logger) : base (patternMatcherFactory, logger)
		{
		}

		protected override FilteredCompletionModel UpdateCompletionList (IAsyncCompletionSession session, AsyncCompletionSessionDataSnapshot data, CancellationToken token)
		{
			switch (data.Trigger.Reason) {
			case CompletionTriggerReason.Deletion:
			case CompletionTriggerReason.Insertion:
			case CompletionTriggerReason.Backspace:
			case CompletionTriggerReason.Invoke:
				if (session.Properties.TryGetProperty (typeof (MSBuildCompletionSource.NuGetSearchUpdater), out MSBuildCompletionSource.NuGetSearchUpdater searchInfo)) {
					//don't pass the CancellationToken to the search job, else filtering operations will cancel searches
					var newList = searchInfo.Update (this, data);

					if (newList.Length != data.InitialSortedList.Length) {
						data = new AsyncCompletionSessionDataSnapshot (
							newList, data.Snapshot, data.Trigger, data.InitialTrigger, data.SelectedFilters, data.IsSoftSelected, data.DisplaySuggestionItem
						);
					}
				}
				break;
			}

			return base.UpdateCompletionList (session, data, token);
		}
	}
}
