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
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class MSBuildCompletionSource : XmlCompletionSource<MSBuildBackgroundParser, MSBuildParseResult>, ICompletionDocumentationProvider
	{
		readonly MSBuildCompletionSourceProvider provider;

		public MSBuildCompletionSource (ITextView textView, MSBuildCompletionSourceProvider provider) : base (textView)
		{
			this.provider = provider;
		}

		class MSBuildCompletionSessionContext
		{
			public MSBuildRootDocument doc;
			public MSBuildResolveResult rr;
			public XmlParser spine;
		}

		// this is primarily used to pass info from GetCompletionContextAsync to GetDocumentationAsync
		// but also reuses the values calculated for expression completion in GetCompletionContextAsync
		// if it's determined not be be expression completion but actually ends up
		// in GetElementCompletionsAsync or GetAttributeCompletionsAsync
		async Task<MSBuildCompletionSessionContext> GetSessionContext (IAsyncCompletionSession session, SnapshotPoint triggerLocation, CancellationToken token)
		{
			if (session.Properties.TryGetProperty<MSBuildCompletionSessionContext> (typeof (MSBuildCompletionSessionContext), out var context)) {
				return context;
			}
			var parser = GetParser ();
			var parseResult = await parser.GetOrParseAsync ((ITextSnapshot2)triggerLocation.Snapshot, token);
			var doc = parseResult.MSBuildDocument ?? MSBuildRootDocument.Empty;
			var spine = parser.GetSpineParser (triggerLocation);
			var rr = MSBuildResolver.Resolve (GetSpineParser (triggerLocation), triggerLocation.Snapshot.GetTextSource (), doc, provider.FunctionTypeProvider);
			context = new MSBuildCompletionSessionContext { doc = doc, rr = rr, spine = spine };
			session.Properties.AddProperty (typeof (MSBuildCompletionSessionContext), context);
			return context;
		}

		CompletionContext CreateCompletionContext (List<CompletionItem> items)
			=> new CompletionContext (ImmutableArray<CompletionItem>.Empty.AddRange (items), null, InitialSelectionHint.SoftSelection);

		protected override async Task<CompletionContext> GetElementCompletionsAsync (
			IAsyncCompletionSession session,
			SnapshotPoint triggerLocation,
			List<XObject> nodePath,
			bool includeBracket,
			CancellationToken token)
		{
			var context = await GetSessionContext (session, triggerLocation, token);
			var rr = context.rr;
			var doc = context.doc;

			if (rr == null) {
				return CompletionContext.Empty;
			}

			var items = new List<CompletionItem> ();
			//TODO: AddMiscBeginTags (list);

			foreach (var el in rr.GetElementCompletions (doc)) {
				items.Add (CreateCompletionItem (el, doc, rr));
			}

			return CreateCompletionContext (items);
		}

		protected override async Task<CompletionContext> GetAttributeCompletionsAsync (
			IAsyncCompletionSession session,
			SnapshotPoint triggerLocation,
			List<XObject> nodePath,
			IAttributedXObject attributedObject,
			Dictionary<string, string> existingAtts,
			CancellationToken token)
		{
			var context = await GetSessionContext (session, triggerLocation, token);
			var rr = context.rr;
			var doc = context.doc;

			if (rr?.LanguageElement == null) {
				return CompletionContext.Empty;
			}

			var items = new List<CompletionItem> ();

			foreach (var att in rr.GetAttributeCompletions (doc, doc.ToolsVersion)) {
				if (!existingAtts.ContainsKey (att.Name)) {
					items.Add (CreateCompletionItem (att, doc, rr));
				}
			}

			return CreateCompletionContext (items); ;
		}

		CompletionItem CreateCompletionItem (BaseInfo info, MSBuildRootDocument doc, MSBuildResolveResult rr)
		{
			var image = DisplayElementFactory.GetImageElement (info);
			var item = new CompletionItem (info.Name, this, image);
			item.AddDocumentationProvider (this);
			item.Properties.AddProperty (typeof(BaseInfo), info);
			return item;
		}

		Task<object> ICompletionDocumentationProvider.GetDocumentationAsync (IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
		{
			if (!item.Properties.TryGetProperty<BaseInfo> (typeof (BaseInfo), out var info) || info == null) {
				return Task.FromResult<object> (null);
			}

			if (!session.Properties.TryGetProperty<MSBuildCompletionSessionContext> (typeof (MSBuildCompletionSessionContext), out var context)) {
				return Task.FromResult<object> (null);
			}

			return DisplayElementFactory.GetInfoTooltipElement (context.doc, info, context.rr, token);
		}

		public override CompletionStartData InitializeCompletion (CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
		{
			//we don't care need a real document here we're doing very basic resolution for triggering
			var spine = GetSpineParser (triggerLocation);
			var rr = MSBuildResolver.Resolve (spine, triggerLocation.Snapshot.GetTextSource (), MSBuildRootDocument.Empty, null);
			if (rr?.LanguageElement != null) {
				if (ExpressionCompletion.IsPossibleExpressionCompletionContext (spine)) {
					string expression = GetAttributeOrElementValueToCaret (spine, triggerLocation);
					var triggerState = ExpressionCompletion.GetTriggerState (
						expression,
						rr.IsCondition (),
						out int triggerLength,
						out ExpressionNode triggerExpression,
						out IReadOnlyList<ExpressionNode> comparandVariables
					);
					if (triggerState != ExpressionCompletion.TriggerState.None) {
						return new CompletionStartData (CompletionParticipation.ProvidesItems, new SnapshotSpan (triggerLocation.Snapshot, triggerLocation.Position - triggerLength, triggerLength));
					}
				}
			}

			return base.InitializeCompletion (trigger, triggerLocation, token);
		}

		public override async Task<CompletionContext> GetCompletionContextAsync (IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
		{
			var context = await GetSessionContext (session, triggerLocation, token);
			var rr = context.rr;
			var doc = context.doc;
			var spine = context.spine;

			if (rr?.LanguageElement != null) {
				if (ExpressionCompletion.IsPossibleExpressionCompletionContext (spine)) {
					string expression = GetAttributeOrElementValueToCaret (spine, triggerLocation);
					var triggerState = ExpressionCompletion.GetTriggerState (
						expression,
						rr.IsCondition (),
						out int triggerLength,
						out ExpressionNode triggerExpression,
						out IReadOnlyList<ExpressionNode> comparandVariables
					);
					if (triggerState != ExpressionCompletion.TriggerState.None) {
						var info = rr.GetElementOrAttributeValueInfo (doc);
						if (info != null && info.ValueKind != MSBuildValueKind.Nothing) {
							return await GetExpressionCompletionsAsync (info, triggerState, triggerLength, triggerExpression, comparandVariables, rr, triggerLocation, doc, token);
						}
					}
				}
			}

			return await base.GetCompletionContextAsync (session, trigger, triggerLocation, applicableToSpan, token);
		}

		Task<CompletionContext> GetPackageNameCompletions (MSBuildRootDocument doc, int startIdx, int triggerLength)
		{
			/*
			string name = ((IXmlParserContext)Tracker.Engine).KeywordBuilder.ToString ();
			if (string.IsNullOrWhiteSpace (name)) {
				return null;
			}

			return Task.FromResult<ICompletionDataList> (
				new PackageNameSearchCompletionDataList (name, PackageSearchManager, doc.GetTargetFrameworkNuGetSearchParameter ()) {
					TriggerWordStart = startIdx,
					TriggerWordLength = triggerLength
				}
			);
			*/
			return Task.FromResult (CompletionContext.Empty);
		}

		Task<CompletionContext> GetPackageVersionCompletions (MSBuildRootDocument doc, MSBuildResolveResult rr, int startIdx, int triggerLength)
		{
			/*
			var name = rr.XElement.Attributes.FirstOrDefault (a => a.Name.FullName == "Include")?.Value;
			if (string.IsNullOrEmpty (name)) {
				return null;
			}
			return Task.FromResult<ICompletionDataList> (
				new PackageVersionSearchCompletionDataList (PackageSearchManager, doc.GetTargetFrameworkNuGetSearchParameter (), name) {
					TriggerWordStart = startIdx,
					TriggerWordLength = triggerLength
				}
			);
			*/
			return Task.FromResult (CompletionContext.Empty);
		}

		Task<CompletionContext> GetSdkCompletions (int triggerLength, CancellationToken token)
		{
			/*
			var list = new CompletionDataList { TriggerWordLength = triggerLength };
			var doc = GetDocument ();
			if (doc == null) {
				return null;
			}

			var sdks = new HashSet<string> ();

			foreach (var sdk in doc.RuntimeInformation.GetRegisteredSdks ()) {
				if (sdks.Add (sdk.Name)) {
					list.Add (Path.GetFileName (sdk.Name));
				}
			}

			//TODO: how can we find SDKs in the non-default locations?
			return Task.Run<ICompletionDataList> (() => {
				foreach (var d in Directory.GetDirectories (doc.RuntimeInformation.GetSdksPath ())) {
					string name = Path.GetFileName (d);
					if (sdks.Add (name)) {
						list.Add (name);
					}
				}
				return list;
			}, token);
			*/
			return Task.FromResult (CompletionContext.Empty);
		}

		async Task<CompletionContext> GetExpressionCompletionsAsync (ValueInfo info, ExpressionCompletion.TriggerState triggerState, int triggerLength, ExpressionNode triggerExpression, IReadOnlyList<ExpressionNode> comparandVariables, MSBuildResolveResult rr, SnapshotPoint triggerLocation, MSBuildRootDocument doc, CancellationToken token)
		{
			var kind = MSBuildCompletionExtensions.InferValueKindIfUnknown (info);

			if (!ExpressionCompletion.ValidateListPermitted (ref triggerState, kind)) {
				return CompletionContext.Empty;
			}

			bool allowExpressions = kind.AllowExpressions ();

			kind = kind.GetScalarType ();

			if (kind == MSBuildValueKind.Data || kind == MSBuildValueKind.Nothing) {
				return CompletionContext.Empty;
			}

			var items = new List<CompletionItem> ();

			if (comparandVariables != null && triggerState == ExpressionCompletion.TriggerState.Value) {
				foreach (var ci in ExpressionCompletion.GetComparandCompletions (doc, comparandVariables)) {
					items.Add (CreateCompletionItem (ci, doc, rr));
				}
			}

			if (triggerState == ExpressionCompletion.TriggerState.Value) {
				switch (kind) {
				case MSBuildValueKind.NuGetID:
					return await GetPackageNameCompletions (doc, triggerLocation.Position - triggerLength, triggerLength);
				case MSBuildValueKind.NuGetVersion:
					return await GetPackageVersionCompletions (doc, rr, triggerLocation.Position - triggerLength, triggerLength);
				case MSBuildValueKind.Sdk:
				case MSBuildValueKind.SdkWithVersion:
					return await GetSdkCompletions (triggerLength, token);
				case MSBuildValueKind.Guid:
					items.Add (CreateNewGuidCompletionItem ());
					break;
				case MSBuildValueKind.Lcid:
					foreach (var culture in System.Globalization.CultureInfo.GetCultures (System.Globalization.CultureTypes.AllCultures)) {
						string name = culture.Name;
						string id = culture.LCID.ToString ();
						string display = culture.DisplayName;
						//TODO: LCID values
						//insert multiple versions for matching on both the name and the number
						//list.Add (new CompletionData (id, null, display));
						//list.Add (new CompletionData (display, null, id, id));
					}
					break;
				}
			}

			//TODO: better metadata support
			IEnumerable<BaseInfo> cinfos;
			if (info.Values != null && info.Values.Count > 0 && triggerState == ExpressionCompletion.TriggerState.Value) {
				cinfos = info.Values;
			} else {
				//FIXME: can we avoid awaiting this unless we actually need to resolve a function? need to propagate async downwards
				await provider.FunctionTypeProvider.EnsureInitialized (token);
				cinfos = ExpressionCompletion.GetCompletionInfos (rr, triggerState, kind, triggerExpression, triggerLength, doc, provider.FunctionTypeProvider);
			}

			if (cinfos != null) {
				foreach (var ci in cinfos) {
					items.Add (CreateCompletionItem (ci, doc, rr));
				}
			}

			if (allowExpressions && triggerState == ExpressionCompletion.TriggerState.Value) {
				//FIXME port this
				//list.Add (new CompletionDataWithSkipCharAndRetrigger ("$(", "md-variable", "Property value reference", "$(|)", ')'));
				//list.Add (new CompletionDataWithSkipCharAndRetrigger ("@(", "md-variable", "Item list reference", "@(|)", ')'));
			}

			if (items.Count > 0) {
				return CreateCompletionContext (items);
			}

			return CompletionContext.Empty;
		}

		CompletionItem CreateNewGuidCompletionItem ()
		{
			var item = new CompletionItem ("New GUID", this, DisplayElementFactory.GetImageElement (KnownImages.Add));
			item.Properties.AddProperty (typeof (MSBuildSpecialCompletionKind), MSBuildSpecialCompletionKind.NewGuid);
			item.AddDocumentation ("Inserts a new GUID");
			return item;
		}
	}

	enum MSBuildSpecialCompletionKind
	{
		NewGuid
	}
}
 
 
 
 