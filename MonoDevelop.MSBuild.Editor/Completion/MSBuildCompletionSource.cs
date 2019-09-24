// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
using MonoDevelop.MSBuild.PackageSearch;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.SdkResolution;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;
using ProjectFileTools.NuGetSearch.Feeds;

using static MonoDevelop.MSBuild.Language.ExpressionCompletion;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	partial class MSBuildCompletionSource : XmlCompletionSource<MSBuildBackgroundParser, MSBuildParseResult>, ICompletionDocumentationProvider
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
			var parseResult = await parser.GetOrParseAsync (triggerLocation.Snapshot, token);
			var doc = parseResult.MSBuildDocument ?? MSBuildRootDocument.Empty;
			var spine = parser.GetSpineParser (triggerLocation);
			// clone the spine because the resolver alters it
			var rr = MSBuildResolver.Resolve ((XmlParser)((ICloneable)spine).Clone (), triggerLocation.Snapshot.GetTextSource (), doc, provider.FunctionTypeProvider, token);
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
			var doc = context.doc;

			// we can't use the LanguageElement from the resolveresult here.
			// if completion is triggered in an existing element's name, the resolveresult
			// will be for that element, so completion will be for the element's children
			// rather than for the element itself.
			MSBuildLanguageElement languageElement = null;
			string elName = null;
			for (int i = 0; i < nodePath.Count; i++) {
				if (nodePath[i] is XElement el) {
					elName = el.Name.Name;
					languageElement = MSBuildLanguageElement.Get (elName, languageElement);
					continue;
				}
				return CompletionContext.Empty;
			}

			if (languageElement == null && nodePath.Count > 0) {
				return CompletionContext.Empty;
			}

			var items = new List<CompletionItem> ();

			foreach (var el in doc.GetElementCompletions (languageElement, elName)) {
				if (el is ItemInfo) {
					items.Add (CreateCompletionItem (el, XmlCompletionItemKind.SelfClosingElement, includeBracket ? "<" : null));
				} else {
					items.Add (CreateCompletionItem (el, XmlCompletionItemKind.Element, includeBracket ? "<" : null));
				}
			}

			bool allowcData = languageElement != null && languageElement.ValueKind != MSBuildValueKind.Nothing;
			foreach (var c in GetMiscellaneousTags (triggerLocation, nodePath, includeBracket, allowcData)) {
				items.Add (c);
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
					items.Add (CreateCompletionItem (att, XmlCompletionItemKind.Attribute));
				}
			}

			return CreateCompletionContext (items);
		}

		CompletionItem CreateCompletionItem (BaseInfo info, XmlCompletionItemKind xmlCompletionItemKind, string prefix = null)
		{
			var image = provider.DisplayElementFactory.GetImageElement (info);
			var item = new CompletionItem (prefix == null ? info.Name : prefix + info.Name, this, image);
			item.AddDocumentationProvider (this);
			item.AddKind (xmlCompletionItemKind);
			item.Properties.AddProperty (typeof (BaseInfo), info);
			return item;
		}

		Task<object> ICompletionDocumentationProvider.GetDocumentationAsync (
			IAsyncCompletionSession session, CompletionItem item,
			CancellationToken token)
		{
			if (!session.Properties.TryGetProperty<MSBuildCompletionSessionContext> (typeof (MSBuildCompletionSessionContext), out var context)) {
				return Task.FromResult<object> (null);
			}

			// note that the value is a tuple despite the key
			if (item.Properties.TryGetProperty<Tuple<string, FeedKind>> (typeof (Tuple<string, FeedKind>), out var packageSearchResult)) {
				return GetPackageDocumentationAsync (context.doc, packageSearchResult.Item1, packageSearchResult.Item2, token);
			}

			if (item.Properties.TryGetProperty<BaseInfo> (typeof (BaseInfo), out var info) && info != null) {
				return provider.DisplayElementFactory.GetInfoTooltipElement (context.doc, info, context.rr, token);
			}

			return Task.FromResult<object> (null);
		}

		async Task<object> GetPackageDocumentationAsync (MSBuildRootDocument doc, string packageId, FeedKind feedKind, CancellationToken token)
		{
			var tfm = doc.GetTargetFrameworkNuGetSearchParameter ();
			var packageInfos = await provider.PackageSearchManager.SearchPackageInfo (packageId, null, tfm).ToTask (token);
			var packageInfo = packageInfos.FirstOrDefault (p => p.SourceKind == feedKind) ?? packageInfos.FirstOrDefault ();
			if (packageInfo != null) {
				return provider.DisplayElementFactory.GetPackageInfoTooltip (packageId, packageInfo, feedKind);
			}
			return null;
		}

		public override CompletionStartData InitializeCompletion (CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
		{
			//we don't care need a real document here we're doing very basic resolution for triggering
			var spine = GetSpineParser (triggerLocation);
			var rr = MSBuildResolver.Resolve (spine, triggerLocation.Snapshot.GetTextSource (), MSBuildRootDocument.Empty, null, token);
			if (rr?.LanguageElement != null) {
				var reason = ConvertReason (trigger.Reason, trigger.Character);
				if (reason.HasValue && IsPossibleExpressionCompletionContext (spine)) {
					string expression = GetAttributeOrElementValueToCaret (spine, triggerLocation);
					var triggerState = GetTriggerState (
						expression,
						reason.Value,
						trigger.Character,
						rr.IsCondition (),
						out int triggerLength,
						out ExpressionNode _,
						out var _,
						out IReadOnlyList<ExpressionNode> _
					);
					if (triggerState != TriggerState.None) {
						return new CompletionStartData (CompletionParticipation.ProvidesItems, new SnapshotSpan (triggerLocation.Snapshot, triggerLocation.Position - triggerLength, triggerLength));
					}
				}
			}

			return base.InitializeCompletion (trigger, triggerLocation, token);
		}

		static TriggerReason? ConvertReason (CompletionTriggerReason reason, char typedChar)
		{
			switch (reason) {
			case CompletionTriggerReason.Insertion:
				if (typedChar != '\0')
					return TriggerReason.TypedChar;
				break;
			case CompletionTriggerReason.Backspace:
				return TriggerReason.Backspace;
			case CompletionTriggerReason.Invoke:
			case CompletionTriggerReason.InvokeAndCommitIfUnique:
				return TriggerReason.Invocation;
			}
			return null;
		}

		public override async Task<CompletionContext> GetCompletionContextAsync (IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
		{
			var context = await GetSessionContext (session, triggerLocation, token);
			var rr = context.rr;
			var doc = context.doc;
			var spine = context.spine;

			if (rr?.LanguageElement != null) {
				var reason = ConvertReason (trigger.Reason, trigger.Character);
				if (reason.HasValue && IsPossibleExpressionCompletionContext (spine)) {
					string expression = GetAttributeOrElementValueToCaret (spine, triggerLocation);
					var triggerState = GetTriggerState (
						expression,
						reason.Value,
						trigger.Character,
						rr.IsCondition (),
						out int triggerLength,
						out ExpressionNode triggerExpression,
						out var listKind,
						out IReadOnlyList<ExpressionNode> comparandVariables
					);
					if (triggerState != TriggerState.None) {
						var info = rr.GetElementOrAttributeValueInfo (doc);
						if (info != null && info.ValueKind != MSBuildValueKind.Nothing) {
							session.Properties.AddProperty (typeof (TriggerState), triggerState);
							return await GetExpressionCompletionsAsync (
								session, info, triggerState, listKind, triggerLength, triggerExpression, comparandVariables, rr, triggerLocation, doc, token);
						}
					}
				}
			}

			return await base.GetCompletionContextAsync (session, trigger, triggerLocation, applicableToSpan, token);
		}

		async Task<List<CompletionItem>> GetPackageNameCompletions (IAsyncCompletionSession session, MSBuildRootDocument doc, string searchQuery, CancellationToken token)
		{
			var tfm = doc.GetTargetFrameworkNuGetSearchParameter ();
			session.Properties.AddProperty (typeof (NuGetSearchUpdater), new NuGetSearchUpdater (this, session, tfm));

			if (string.IsNullOrEmpty (searchQuery)) {
				return null;
			}

			var results = await provider.PackageSearchManager.SearchPackageNames (searchQuery.ToLower (), tfm).ToTask (token);

			return CreateNuGetItemsFromSearchResults (results);
		}

		List<CompletionItem> CreateNuGetItemsFromSearchResults (IReadOnlyList<Tuple<string, FeedKind>> results)
		{
			var items = new List<CompletionItem> ();
			var dedup = new HashSet<string> ();

			// dedup, preferring nuget -> myget -> local
			AddItems (FeedKind.NuGet);
			AddItems (FeedKind.MyGet);
			AddItems (FeedKind.Local);

			void AddItems (FeedKind kind)
			{
				foreach (var result in results) {
					if (result.Item2 == kind) {
						if (dedup.Add (result.Item1)) {
							items.Add (CreateNuGetCompletionItem (result, XmlCompletionItemKind.AttributeValue));
						}
					}
				}
			}

			return items;
		}

		async Task<List<CompletionItem>> GetPackageVersionCompletions (MSBuildRootDocument doc, MSBuildResolveResult rr, CancellationToken token)
		{
			if (rr == null) {
				return null;
			}

			var packageId = rr.XElement.Attributes.FirstOrDefault (a => a.Name.Name == "Include")?.Value;
			if (string.IsNullOrEmpty (packageId)) {
				return null;
			}

			var tfm = doc.GetTargetFrameworkNuGetSearchParameter ();

			var results = await provider.PackageSearchManager.SearchPackageVersions (packageId.ToLower (), tfm).ToTask (token);

			//FIXME should we deduplicate?
			var items = new List<CompletionItem> ();
			foreach (var result in results) {
				items.Add (CreateNuGetCompletionItem (result, XmlCompletionItemKind.AttributeValue));
			}

			return items;
		}

		//FIXME: SDK version completion
		//FIXME: enumerate SDKs from NuGet
		Task<List<CompletionItem>> GetSdkCompletions (MSBuildRootDocument doc, CancellationToken token)
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
					if (!Directory.Exists (sdkDir)) {
						return;
					}
					foreach (var dir in Directory.GetDirectories (sdkDir)) {
						string name = Path.GetFileName (dir);
						var targetsFileExists = File.Exists (Path.Combine (dir, "Sdk", "Sdk.targets"));
						if (targetsFileExists && sdks.Add (name)) {
							items.Add (CreateSdkCompletionItem (new SdkInfo (name, null, Path.Combine (dir, name))));
						}
					}
				}

					return items;
				}, token);
		}

		async Task<CompletionContext> GetExpressionCompletionsAsync (
			IAsyncCompletionSession session,
			ValueInfo info, TriggerState triggerState, ListKind listKind,
			int triggerLength, ExpressionNode triggerExpression,
			IReadOnlyList<ExpressionNode> comparandVariables,
			MSBuildResolveResult rr, SnapshotPoint triggerLocation,
			MSBuildRootDocument doc, CancellationToken token)
		{
			var kind = info.InferValueKindIfUnknown ();

			if (!ValidateListPermitted (listKind, kind)) {
				return CompletionContext.Empty;
			}

			bool allowExpressions = kind.AllowExpressions ();

			kind = kind.GetScalarType ();

			if (kind == MSBuildValueKind.Data || kind == MSBuildValueKind.Nothing) {
				return CompletionContext.Empty;
			}

			bool isValue = triggerState == TriggerState.Value;

			var items = new List<CompletionItem> ();

			if (comparandVariables != null && isValue) {
				foreach (var ci in ExpressionCompletion.GetComparandCompletions (doc, comparandVariables)) {
					items.Add (CreateCompletionItem (ci, XmlCompletionItemKind.AttributeValue));
				}
			}

		if (isValue) {
				switch (kind) {
				case MSBuildValueKind.NuGetID:
					if (triggerExpression is ExpressionText t) {
						var packageNameItems = await GetPackageNameCompletions (session, doc, t.Value, token);
						if (packageNameItems != null) {
							items.AddRange (packageNameItems);
						}
					}
					break;
				case MSBuildValueKind.NuGetVersion: {
						var packageVersionItems = await GetPackageVersionCompletions (doc, rr, token);
						if (packageVersionItems != null) {
							items.AddRange (packageVersionItems);
						}
						break;
					}
				case MSBuildValueKind.Sdk:
				case MSBuildValueKind.SdkWithVersion: {
					var sdkItems = await GetSdkCompletions (doc, token);
						if (sdkItems != null) {
							items.AddRange (sdkItems);
						}
						break;
					}
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
					items.Add (CreateCompletionItem (ci, XmlCompletionItemKind.AttributeValue));
				}
			}

			if ((allowExpressions && isValue) || triggerState == TriggerState.BareFunctionArgumentValue) {
				items.Add (CreateSpecialItem ("$(", "Property reference", KnownImages.MSBuildProperty, MSBuildSpecialCommitKind.PropertyReference));
			}

			if (allowExpressions && isValue) {
				items.Add (CreateSpecialItem ("@(", "Item reference", KnownImages.MSBuildItem, MSBuildSpecialCommitKind.ItemReference));
				if (IsMetadataAllowed (triggerExpression, rr)) {
					items.Add (CreateSpecialItem ("%(", "Metadata reference", KnownImages.MSBuildItem, MSBuildSpecialCommitKind.MetadataReference));
				}
			}

			if (items.Count > 0) {
				return CreateCompletionContext (items);
			}

			return CompletionContext.Empty;
		}

		//FIXME: improve logic for determining where metadata is permitted
		bool IsMetadataAllowed (ExpressionNode triggerExpression, MSBuildResolveResult rr)
		{
			//if any a parent node is an item transform or function, metadata is allowed
			if (triggerExpression != null) {
				var node = triggerExpression.Find (triggerExpression.Length);
				while (node != null) {
					if (node is ExpressionItemTransform || node is ExpressionItemFunctionInvocation) {
						return true;
					}
					node = node.Parent;
				}
			}

			if (rr.LanguageAttribute != null) {
				switch (rr.LanguageAttribute.SyntaxKind) {
				// metadata attributes on items can refer to other metadata on the items
				case MSBuildSyntaxKind.Item_Metadata:
				// task params can refer to metadata in batched items
				case MSBuildSyntaxKind.Task_Parameter:
				// target inputs and outputs can use metadata from each other's items
				case MSBuildSyntaxKind.Target_Inputs:
				case MSBuildSyntaxKind.Target_Outputs:
					return true;
				//conditions on metadata elements can refer to metadata on the items
				case MSBuildSyntaxKind.Metadata_Condition:
					return true;
				}
			}

			if (rr.LanguageElement != null) {
				switch (rr.LanguageElement.SyntaxKind) {
				// metadata elements can refer to other metadata in the items
				case MSBuildSyntaxKind.Metadata:
					return true;
				}
			}
			return false;
		}

		CompletionItem CreateSpecialItem (string text, string description, KnownImages image, MSBuildSpecialCommitKind kind)
		{
			var item = new CompletionItem (text, this, provider.DisplayElementFactory.GetImageElement (image));
			item.Properties.AddProperty (typeof (MSBuildSpecialCommitKind), kind);
			item.AddDocumentation (description);
			return item;
		}

		CompletionItem CreateNuGetCompletionItem (Tuple<string,FeedKind> info, XmlCompletionItemKind xmlCompletionItemKind)
		{
			var kindImage = provider.DisplayElementFactory.GetImageElement (info.Item2);
			var item = new CompletionItem (info.Item1, this, kindImage);
			item.AddKind (xmlCompletionItemKind);
			item.Properties.AddProperty (typeof (Tuple<string, FeedKind>), info);
			item.AddDocumentationProvider (this);
			return item;
		}

		CompletionItem CreateSdkCompletionItem (SdkInfo info)
		{
			var img = provider.DisplayElementFactory.GetImageElement (KnownImages.Sdk);
			var item = new CompletionItem (info.Name, this, img);
			//FIXME better tooltips for SDKs
			item.AddDocumentation (info.Path);
			return item;
		}

		IEnumerable<CompletionItem> GetLcidCompletions ()
		{
			var imageEl = provider.DisplayElementFactory.GetImageElement (KnownImages.Constant);
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
		ItemReference,
		MetadataReference
	}
}
