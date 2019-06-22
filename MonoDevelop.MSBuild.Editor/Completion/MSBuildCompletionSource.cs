// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class MSBuildCompletionSource : XmlCompletionSource<MSBuildBackgroundParser, MSBuildParseResult>, ICompletionDocumentationProvider
	{
		public MSBuildCompletionSource (ITextView textView) : base (textView)
		{
		}

		class MSBuildCompletionSessionContext
		{
			public MSBuildRootDocument doc;
			public MSBuildResolveResult rr;
		}

		async Task<(MSBuildRootDocument doc, MSBuildResolveResult rr)> GetSessionContext (IAsyncCompletionSession session, SnapshotPoint triggerLocation, CancellationToken token)
		{
			if (session.Properties.TryGetProperty<MSBuildCompletionSessionContext> (typeof (MSBuildCompletionSessionContext), out var context)) {
				return (context.doc, context.rr);
			}
			var parseResult = await GetParseAsync (triggerLocation.Snapshot, token);
			var doc = parseResult.MSBuildDocument ?? MSBuildRootDocument.Empty;
			var rr = ResolveAt (triggerLocation, doc);
			session.Properties.AddProperty (typeof (MSBuildCompletionSessionContext), new MSBuildCompletionSessionContext { doc = doc, rr = rr });
			return (doc, rr);
		}

		protected override async Task<CompletionContext> GetElementCompletionsAsync (
			IAsyncCompletionSession session,
			SnapshotPoint triggerLocation,
			List<XObject> nodePath,
			bool includeBracket,
			CancellationToken token)
		{
			(var doc, var rr) = await GetSessionContext (session, triggerLocation, token);
			if (rr == null) {
				return CompletionContext.Empty;
			}

			var items = new List<CompletionItem> ();
			//TODO: AddMiscBeginTags (list);

			foreach (var el in rr.GetElementCompletions (doc)) {
				items.Add (CreateCompletionItem (el, doc, rr));
			}

			return new CompletionContext (ImmutableArray<CompletionItem>.Empty.AddRange (items));
		}

		protected override async Task<CompletionContext> GetAttributeCompletionsAsync (
			IAsyncCompletionSession session,
			SnapshotPoint triggerLocation,
			List<XObject> nodePath,
			IAttributedXObject attributedObject,
			Dictionary<string, string> existingAtts,
			CancellationToken token)
		{
			(var doc, var rr) = await GetSessionContext (session, triggerLocation, token);
			if (rr?.LanguageElement == null) {
				return CompletionContext.Empty;
			}

			var items = new List<CompletionItem> ();

			foreach (var att in rr.GetAttributeCompletions (doc, doc.ToolsVersion)) {
				if (!existingAtts.ContainsKey (att.Name)) {
					items.Add (CreateCompletionItem (att, doc, rr));
				}
			}

			return new CompletionContext (ImmutableArray<CompletionItem>.Empty.AddRange (items));
		}

		CompletionItem CreateCompletionItem (BaseInfo info, MSBuildRootDocument doc, MSBuildResolveResult rr)
		{
			var image = DisplayElementFactory.GetImageElement (info);
			var item = new CompletionItem (info.Name, this, image);
			item.AddDocumentationProvider (this);
			item.Properties.AddProperty (typeof(BaseInfo), info);
			return item;
		}

		MSBuildResolveResult ResolveAt (SnapshotPoint point, MSBuildDocument context) =>
			MSBuildResolver.Resolve (GetSpineParser (point), point.Snapshot.GetTextSource (), context);

		Task<MSBuildParseResult> GetParseAsync (ITextSnapshot snapshot, CancellationToken cancellationToken) => GetParser ().GetOrParseAsync ((ITextSnapshot2) snapshot, cancellationToken);

		Task<object> ICompletionDocumentationProvider.GetDocumentationAsync (IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
		{
			if (!item.Properties.TryGetProperty<BaseInfo> (typeof (BaseInfo), out var info) || info == null) {
				return Task.FromResult<object> (null);
			}

			if (!session.Properties.TryGetProperty<MSBuildCompletionSessionContext> (typeof (MSBuildCompletionSessionContext), out var context)) {
				return Task.FromResult<object> (null);
			}

			return Task.FromResult (DisplayElementFactory.GetInfoTooltipElement (context.doc, info, context.rr));
		}
	}
}