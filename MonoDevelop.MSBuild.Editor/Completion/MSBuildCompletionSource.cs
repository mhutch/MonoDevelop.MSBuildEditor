// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.Framework;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.SdkResolution;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

using ProjectFileTools.NuGetSearch.Feeds;

using static MonoDevelop.MSBuild.Language.ExpressionCompletion;

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
						trigger.Character,
						rr.IsCondition (),
						out int triggerLength,
						out ExpressionNode triggerExpression,
						out var listKind,
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
						trigger.Character,
						rr.IsCondition (),
						out int triggerLength,
						out ExpressionNode triggerExpression,
						out var listKind,
						out IReadOnlyList<ExpressionNode> comparandVariables
					);
					if (triggerState != ExpressionCompletion.TriggerState.None) {
						var info = rr.GetElementOrAttributeValueInfo (doc);
						if (info != null && info.ValueKind != MSBuildValueKind.Nothing) {
							return await GetExpressionCompletionsAsync (info, triggerState, listKind, triggerLength, triggerExpression, comparandVariables, rr, triggerLocation, doc, token);
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

		async Task<CompletionContext> GetPackageVersionCompletions (MSBuildRootDocument doc, MSBuildResolveResult rr)
		{
			var packageId = rr.XElement.Attributes.FirstOrDefault (a => a.Name.Name == "Include")?.Value;
			if (string.IsNullOrEmpty (packageId)) {
				return null;
			}

			var tfm = doc.GetTargetFrameworkNuGetSearchParameter ();
			var search = provider.PackageSearchManager.SearchPackageVersions (packageId.ToLower (), tfm);
			var tcs = new TaskCompletionSource<object> ();
			search.Updated += (s, e) => { if (search.RemainingFeeds.Count == 0) tcs.TrySetResult (null); };
			await tcs.Task;

			//FIXME should we deduplicate?
			var items = new List<CompletionItem> ();
			foreach (var result in search.Results) {
				items.Add (CreateNuGetVersionCompletionItem (result.Item1, result.Item2));
			}

			return CreateCompletionContext (items);
		}

		//FIXME: SDK version completion
		//FIXME: enumerate SDKs from NuGet
		Task<CompletionContext> GetSdkCompletions (MSBuildRootDocument doc, CancellationToken token)
		{
			return Task.Run (() => {
				var items = new List<CompletionItem> ();
				var sdks = new HashSet<string> ();

				foreach (var sdk in doc.RuntimeInformation.GetRegisteredSdks ()) {
					if (sdks.Add (sdk.Name)) {
						items.Add (CreateSdkCompletionItem (sdk));
					}
				}

				//FIXME we should be able to cache these
				var sdksPath = doc.RuntimeInformation.SdksPath;
				if (sdksPath != null) {
					AddSdksFromDir (sdksPath);
				}

				var dotNetSdkPath = doc.RuntimeInformation.GetSdkPath (new SdkReference ("Microsoft.NET.Sdk", null, null), null, null);
				if (dotNetSdkPath != null) {
					dotNetSdkPath = Path.GetDirectoryName (Path.GetDirectoryName (dotNetSdkPath));
					if (sdksPath == null || Path.GetFullPath (dotNetSdkPath) != Path.GetFullPath (sdksPath)) {
						AddSdksFromDir (dotNetSdkPath);
					}
				}

				void AddSdksFromDir (string sdkDir)
				{
					foreach (var dir in Directory.GetDirectories (sdkDir)) {
						string name = Path.GetFileName (dir);
						var targetsFileExists = File.Exists (Path.Combine (dir, "Sdk", "Sdk.targets"));
							if (targetsFileExists && sdks.Add (name)) {
								items.Add (CreateSdkCompletionItem (new SdkInfo (name, null, Path.Combine (dir, name))));
							}
						}
					}

					return CreateCompletionContext (items);
				}, token);
		}

		async Task<CompletionContext> GetExpressionCompletionsAsync (ValueInfo info, ExpressionCompletion.TriggerState triggerState, ExpressionCompletion.ListKind listKind, int triggerLength, ExpressionNode triggerExpression, IReadOnlyList<ExpressionNode> comparandVariables, MSBuildResolveResult rr, SnapshotPoint triggerLocation, MSBuildRootDocument doc, CancellationToken token)
		{
			var kind = MSBuildCompletionExtensions.InferValueKindIfUnknown (info);

			if (!ExpressionCompletion.ValidateListPermitted (listKind, kind)) {
				return CompletionContext.Empty;
			}

			bool allowExpressions = kind.AllowExpressions ();

			kind = kind.GetScalarType ();

			if (kind == MSBuildValueKind.Data || kind == MSBuildValueKind.Nothing) {
				return CompletionContext.Empty;
			}

			bool isValue = triggerState == ExpressionCompletion.TriggerState.Value
				|| triggerState == ExpressionCompletion.TriggerState.PropertyOrValue
				|| triggerState == ExpressionCompletion.TriggerState.ItemOrValue
				|| triggerState == ExpressionCompletion.TriggerState.MetadataOrValue;

			var items = new List<CompletionItem> ();

			if (comparandVariables != null && isValue) {
				foreach (var ci in ExpressionCompletion.GetComparandCompletions (doc, comparandVariables)) {
					items.Add (CreateCompletionItem (ci, doc, rr));
				}
			}

			if (isValue) {
				switch (kind) {
				case MSBuildValueKind.NuGetID:
					return await GetPackageNameCompletions (doc, triggerLocation.Position - triggerLength, triggerLength);
				case MSBuildValueKind.NuGetVersion:
					return await GetPackageVersionCompletions (doc, rr);
				case MSBuildValueKind.Sdk:
				case MSBuildValueKind.SdkWithVersion:
					return await GetSdkCompletions (doc, token);
				case MSBuildValueKind.Guid:
					items.Add (CreateSpecialItem ("New GUID", "Inserts a new GUID", KnownImages.Add, MSBuildSpecialCommitKind.NewGuid));
					break;
				case MSBuildValueKind.Lcid:
					items.AddRange (GetLcidCompletions ());
					break;
				}
			}

			//TODO: better metadata support
			IEnumerable<BaseInfo> cinfos;
			if (info.Values != null && info.Values.Count > 0 && isValue) {
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

			if ((allowExpressions && isValue) || triggerState == TriggerState.BareFunctionArgumentValue) {
				items.Add (CreateSpecialItem ("$(", "Property reference", KnownImages.MSBuildProperty, MSBuildSpecialCommitKind.PropertyReference));
			}

			if (allowExpressions && isValue) {
				items.Add (CreateSpecialItem ("@(", "Item reference", KnownImages.MSBuildItem, MSBuildSpecialCommitKind.ItemReference));
				//FIXME metadata
			}

			if (items.Count > 0) {
				return CreateCompletionContext (items);
			}

			return CompletionContext.Empty;
		}

		CompletionItem CreateSpecialItem (string text, string description, KnownImages image, MSBuildSpecialCommitKind kind)
		{
			var item = new CompletionItem (text, this, DisplayElementFactory.GetImageElement (image));
			item.Properties.AddProperty (typeof (MSBuildSpecialCommitKind), kind);
			item.AddDocumentation (description);
			return item;
		}

		KnownImages GetPackageImageId (FeedKind kind)
		{
			switch (kind) {
			case FeedKind.Local: return KnownImages.FolderClosed;
			case FeedKind.NuGet: return KnownImages.NuGet;
			default: return KnownImages.GenericNuGetPackage;
			}
		}

		CompletionItem CreateNuGetVersionCompletionItem (string version, FeedKind kind)
		{
			var kindImage = DisplayElementFactory.GetImageElement (GetPackageImageId (kind));
			var item = new CompletionItem (version, this, kindImage);
			return item;
		}

		CompletionItem CreateSdkCompletionItem (SdkInfo info)
		{
			var img = DisplayElementFactory.GetImageElement (KnownImages.Sdk);
			var item = new CompletionItem (info.Name, this, img);
			//FIXME better tooltips for SDKs
			item.AddDocumentation (info.Path);
			return item;
		}

		IEnumerable<CompletionItem> GetLcidCompletions ()
		{
			var imageEl = DisplayElementFactory.GetImageElement (KnownImages.Constant);
			foreach (var culture in System.Globalization.CultureInfo.GetCultures (System.Globalization.CultureTypes.AllCultures)) {
				string name = culture.Name;
				string id = culture.LCID.ToString ();
				string display = culture.DisplayName;
				string displayText = $"{id} - ({display})";
				yield return new CompletionItem (displayText, this, imageEl, ImmutableArray<CompletionFilter>.Empty, string.Empty, id, id, displayText, ImmutableArray<ImageElement>.Empty);
			}
		}
	}

	enum MSBuildSpecialCommitKind
	{
		NewGuid,
		PropertyReference,
		ItemReference
	}
}
 
 
 
 