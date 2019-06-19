// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Completion;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Editor.IntelliSense;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class MSBuildCompletionSource : XmlCompletionSource<MSBuildBackgroundParser, MSBuildParseResult>, ICompletionDocumentationProvider
	{
		public MSBuildCompletionSource (ITextView textView) : base (textView)
		{
		}

		protected override Task<CompletionContext> GetElementCompletionsAsync (
			SnapshotPoint triggerLocation,
			List<XObject> nodePath,
			bool includeBracket,
			CancellationToken token)
		{
			var doc = GetMSBuildDocument ();
			var rr = ResolveAt (triggerLocation, doc);
			if (rr == null) {
				return Task.FromResult (CompletionContext.Empty);
			}

			var items = new List<CompletionItem> ();
			//AddMiscBeginTags (list);

			foreach (var el in rr.GetElementCompletions (doc)) {
				items.Add (CreateCompletionItem (el, doc, rr, XmlImages.ElementImage));
			}

			return Task.FromResult (new CompletionContext (ImmutableArray<CompletionItem>.Empty.AddRange (items)));
		}

		protected override Task<CompletionContext> GetAttributeCompletionsAsync (
			SnapshotPoint triggerLocation,
			List<XObject> nodePath,
			IAttributedXObject attributedObject,
			Dictionary<string, string> existingAtts,
			CancellationToken token)
		{
			var doc = GetMSBuildDocument ();
			var rr = ResolveAt (triggerLocation, doc);
			if (rr?.LanguageElement == null)
				return Task.FromResult (CompletionContext.Empty);

			var items = new List<CompletionItem> ();

			foreach (var att in rr.GetAttributeCompletions (doc, doc.ToolsVersion)) {
				if (!existingAtts.ContainsKey (att.Name)) {
					items.Add (CreateCompletionItem (att, doc, rr, XmlImages.AttributeImage));
				}
			}

			return Task.FromResult (new CompletionContext (ImmutableArray<CompletionItem>.Empty.AddRange (items)));
		}

		CompletionItem CreateCompletionItem (BaseInfo info, MSBuildRootDocument doc, MSBuildResolveResult rr, Microsoft.VisualStudio.Text.Adornments.ImageElement image)
		{
			//FIXME add description etc
			var item = new CompletionItem (info.Name, this);
			item.AddDocumentationProvider (this);
			item.Properties.AddProperty (typeof(BaseInfo), info);
			return item;
		}

		MSBuildResolveResult ResolveAt (SnapshotPoint point, MSBuildDocument context) =>
			MSBuildResolver.Resolve (GetSpineParser (point), point.Snapshot.GetTextSource (), context);

		MSBuildRootDocument GetMSBuildDocument () => new MSBuildRootDocument (null);

		public Task<object> GetDocumentationAsync (CompletionItem item)
		{
			if (item.Properties.TryGetProperty<BaseInfo> (typeof (BaseInfo), out var info)) {
				return Task.FromResult<object> (
					new ContainerElement (
						ContainerElementStyle.Wrapped,
						new ClassifiedTextElement (
							new ClassifiedTextRun (PredefinedClassificationTypeNames.NaturalLanguage, info.Description.AsText ())
						)
					)
				);
			}
			return Task.FromResult<object> (null);
		}
	}
}