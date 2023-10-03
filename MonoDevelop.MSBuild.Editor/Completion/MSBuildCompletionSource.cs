// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.PackageSearch;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.SdkResolution;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

using ProjectFileTools.NuGetSearch.Feeds;

using static MonoDevelop.MSBuild.Language.ExpressionCompletion;
using MonoDevelop.Xml.Logging;

// todo: switch to IAsyncCompletionUniversalSource to allow per-item span and commit chars
namespace MonoDevelop.MSBuild.Editor.Completion
{
	partial class MSBuildCompletionSource : XmlCompletionSource, ICompletionDocumentationProvider
	{
		readonly MSBuildBackgroundParser parser;
		readonly IMSBuildFileSystem fileSystem;
		readonly MSBuildCompletionSourceProvider provider;

		public MSBuildCompletionSource (ITextView textView, MSBuildCompletionSourceProvider provider, ILogger logger) : base (textView, logger, provider.XmlParserProvider)
		{
			this.provider = provider;
			parser = provider.ParserProvider.GetParser (textView.TextBuffer);
			fileSystem = provider.FileSystem ?? DefaultMSBuildFileSystem.Instance;
        }

		record class MSBuildCompletionSessionContext (MSBuildRootDocument document, MSBuildResolveResult resolved, XmlSpineParser spine);

		// this is primarily used to pass info from GetCompletionContextAsync to GetDocumentationAsync
		// but also reuses the values calculated for expression completion in GetCompletionContextAsync
		// if it's determined not be be expression completion but actually ends up
		// in GetElementCompletionsAsync or GetAttributeCompletionsAsync
		async Task<MSBuildCompletionSessionContext> CreateMSBuildSessionContext (IAsyncCompletionSession session, SnapshotPoint triggerLocation, CancellationToken token)
		{
			MSBuildParseResult parseResult = parser.LastOutput ?? await parser.GetOrProcessAsync (triggerLocation.Snapshot, token);
			var doc = parseResult.MSBuildDocument ?? MSBuildRootDocument.Empty;
			var spine = GetSpineParser (triggerLocation);
			// clone the spine because the resolver alters it
			var rr = MSBuildResolver.Resolve (spine.Clone (), triggerLocation.Snapshot.GetTextSource (), doc, provider.FunctionTypeProvider, Logger, token);
			return new MSBuildCompletionSessionContext (doc, rr, spine);
		}

		void InitializeMSBuildSessionContext (IAsyncCompletionSession session, SnapshotPoint triggerLocation, CancellationToken token)
		{
			session.Properties.AddProperty (typeof (MSBuildCompletionSessionContext), CreateMSBuildSessionContext (session, triggerLocation, token));
		}


		// this can only be called after SetMSBuildSessionContext
		Task<MSBuildCompletionSessionContext> GetMSBuildSessionContext (IAsyncCompletionSession session)
			=> session.Properties.GetProperty<Task<MSBuildCompletionSessionContext>> (typeof (MSBuildCompletionSessionContext));

		public override Task<object> GetDescriptionAsync (IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
			=> Logger.InvokeAndLogExceptions (
				() => base.GetDescriptionAsync (session, item, token));

		protected override async Task<IList<CompletionItem>> GetElementCompletionsAsync (
			IAsyncCompletionSession session,
			SnapshotPoint triggerLocation,
			List<XObject> nodePath,
			bool includeBracket,
			CancellationToken token)
		{
			var context = await GetMSBuildSessionContext (session);
			var doc = context.document;

			// we can't use the LanguageElement from the resolveresult here.
			// if completion is triggered in an existing element's name, the resolveresult
			// will be for that element, so completion will be for the element's children
			// rather than for the element itself.
			MSBuildElementSyntax languageElement = null;
			string elName = null;
			for (int i = 1; i < nodePath.Count; i++) {
				if (nodePath[i] is XElement el) {
					elName = el.Name.Name;
					languageElement = MSBuildElementSyntax.Get (elName, languageElement);
					continue;
				}
				return null;
			}

			if (languageElement == null && nodePath.Count > 0) {
				return null;
			}

			var items = new List<CompletionItem> ();

			foreach (var el in doc.GetElementCompletions (languageElement, elName)) {
				if (el is ItemInfo) {
					items.Add (CreateCompletionItem (el, XmlCompletionItemKind.SelfClosingElement, includeBracket ? "<" : null));
				} else {
					items.Add (CreateCompletionItem (el, XmlCompletionItemKind.Element, includeBracket ? "<" : null));
				}
			}

			bool allowCData = languageElement != null && languageElement.ValueKind != MSBuildValueKind.Nothing;
			foreach (var c in GetMiscellaneousTags (triggerLocation, nodePath, includeBracket, allowCData)) {
				items.Add (c);
			}

			return items;
		}

		protected override async Task<IList<CompletionItem>> GetAttributeCompletionsAsync (
			IAsyncCompletionSession session,
			SnapshotPoint triggerLocation,
			List<XObject> nodePath,
			IAttributedXObject attributedObject,
			Dictionary<string, string> existingAtts,
			CancellationToken token)
		{
			var context = await GetMSBuildSessionContext (session);
			var rr = context.resolved;
			var doc = context.document;

			if (rr?.ElementSyntax == null) {
				return null;
			}

			var items = new List<CompletionItem> ();

			foreach (var att in rr.GetAttributeCompletions (doc, doc.ToolsVersion)) {
				if (!existingAtts.ContainsKey (att.Name)) {
					items.Add (CreateCompletionItem (att, XmlCompletionItemKind.Attribute));
				}
			}

			return items;
		}

		protected override async Task<IList<CompletionItem>> GetEntityCompletionsAsync (IAsyncCompletionSession session, SnapshotPoint triggerLocation, List<XObject> nodePath, CancellationToken token)
		{
			var context = await GetMSBuildSessionContext (session);

			// don't want entity completion in elements we know don't have text content
			if (context.resolved is not null && context.resolved.AttributeName is null && context.resolved.ElementSyntax is MSBuildElementSyntax element && element.ValueKind == MSBuildValueKind.Nothing) {
				return null;
			}

			return GetBuiltInEntityItems ();
		}

		CompletionItem CreateCompletionItem (ISymbol info, XmlCompletionItemKind xmlCompletionItemKind, string prefix = null)
		{
			var image = provider.DisplayElementFactory.GetImageElement (info);
			var item = new CompletionItem (prefix == null ? info.Name : prefix + info.Name, this, image);
			item.AddDocumentationProvider (this);
			item.AddKind (xmlCompletionItemKind);
			item.Properties.AddProperty (typeof (ISymbol), info);
			return item;
		}

		Task<object> ICompletionDocumentationProvider.GetDocumentationAsync (IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
		{
#pragma warning disable VSTHRD103 // we know this is completed here
			var context = GetMSBuildSessionContext (session).Result;
#pragma warning restore VSTHRD103

			// note that the value is a tuple despite the key
			if (item.Properties.TryGetProperty<Tuple<string, FeedKind>> (typeof (Tuple<string, FeedKind>), out var packageSearchResult)) {
				return GetPackageDocumentationAsync (context.document, packageSearchResult.Item1, packageSearchResult.Item2, token);
			}

			if (item.Properties.TryGetProperty<ISymbol> (typeof (ISymbol), out var info) && info != null) {
				return provider.DisplayElementFactory.GetInfoTooltipElement (
					session.TextView.TextBuffer, context.document, info, context.resolved, token
				);
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
			=> Logger.InvokeAndLogExceptions (
				() => InitializeCompletionInternal (trigger, triggerLocation, token));

		CompletionStartData InitializeCompletionInternal (CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
		{
			var baseCompletion = base.InitializeCompletion (trigger, triggerLocation, token);

			//we don't care need a real document here we're doing very basic resolution for triggering
			var spine = GetSpineParser (triggerLocation);
			var rr = MSBuildResolver.Resolve (spine.Clone (), triggerLocation.Snapshot.GetTextSource (), MSBuildRootDocument.Empty, null, Logger, token);
			if (rr?.ElementSyntax is MSBuildElementSyntax elementSyntax && (rr.Attribute is not null || elementSyntax.ValueKind != MSBuildValueKind.Nothing)) {
				var reason = ConvertReason (trigger.Reason, trigger.Character);
				if (reason.HasValue && IsPossibleExpressionCompletionContext (spine)) {
					string expression = spine.GetIncompleteValue (triggerLocation.Snapshot);
					int exprStartPos = triggerLocation - expression.Length;
					var triggerState = GetTriggerState (
						expression,
						triggerLocation - exprStartPos,
						reason.Value,
						trigger.Character,
						rr.IsCondition (),
						out int spanStart,
						out int spanLength,
						out ExpressionNode _,
						out var _,
						out IReadOnlyList<ExpressionNode> _,
						Logger
					);
					if (triggerState != TriggerState.None) {
						spanStart = exprStartPos + spanStart;
						return new CompletionStartData (CompletionParticipation.ProvidesItems, new SnapshotSpan (triggerLocation.Snapshot, spanStart, spanLength));
					}
				}
			}

			return baseCompletion;
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

		public override Task<CompletionContext> GetCompletionContextAsync (IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
			=> Logger.InvokeAndLogExceptions (
				() => GetCompletionContextAsyncInternal (session, trigger, triggerLocation, applicableToSpan, token));

		Task<CompletionContext> GetCompletionContextAsyncInternal (IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
		{
			InitializeMSBuildSessionContext (session, triggerLocation, token);

			return base.GetCompletionContextAsync (session, trigger, triggerLocation, applicableToSpan, token);
		}

		protected override async Task<IList<CompletionItem>> GetAdditionalCompletionsAsync (IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
		{
			var context = await GetMSBuildSessionContext(session).ConfigureAwait (false);

			if (context.resolved?.ElementSyntax is null) {
				return null;
			}

			var reason = ConvertReason (trigger.Reason, trigger.Character);
			if (!reason.HasValue || !IsPossibleExpressionCompletionContext (context.spine)) {
				return null;
			}

			string expression = context.spine.GetIncompleteValue (triggerLocation.Snapshot);
			int exprStartPos = triggerLocation.Position - expression.Length;
			var triggerState = GetTriggerState (expression, triggerLocation - exprStartPos, reason.Value, trigger.Character, context.resolved.IsCondition (),
				out int spanStart, out int spanLength, out ExpressionNode triggerExpression, out var listKind, out IReadOnlyList<ExpressionNode> comparandVariables,
				Logger
			);
			spanStart = exprStartPos + spanStart;

			if (triggerState == TriggerState.None) {
				return null;
			}

			session.Properties.AddProperty (typeof (TriggerState), triggerState);

			var info = context.resolved.GetElementOrAttributeValueInfo (context.document);
			if (info is null || info.ValueKind == MSBuildValueKind.Nothing) {
				return null;
			}

			return await GetExpressionCompletionsAsync (
					session, info, triggerState, listKind, spanLength, triggerExpression, comparandVariables,
					context.resolved, triggerLocation, context.document, token
				);
		}

		async Task<List<CompletionItem>> GetPackageNameCompletions (IAsyncCompletionSession session, MSBuildRootDocument doc, string searchQuery, string packageType, CancellationToken token)
		{
			var tfm = doc.GetTargetFrameworkNuGetSearchParameter ();
			session.Properties.AddProperty (typeof (NuGetSearchUpdater), new NuGetSearchUpdater (this, session, tfm, packageType, Logger));

			if (string.IsNullOrEmpty (searchQuery)) {
				return null;
			}

			var results = await provider.PackageSearchManager.SearchPackageNames (searchQuery.ToLower (), tfm, packageType).ToTask (token);

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

		static bool ItemIsInItemGroup (XElement itemEl) => itemEl.Parent is XElement parent && parent.NameEquals (MSBuildElementSyntax.ItemGroup.Name, true);

		static XElement GetItemGroupItemFromMetadata (MSBuildResolveResult rr)
			=> rr.ElementSyntax.SyntaxKind switch {
				MSBuildSyntaxKind.Item => rr.Element,
				MSBuildSyntaxKind.Metadata => rr.Element.Parent is XElement parentEl && ItemIsInItemGroup (parentEl)? parentEl : null,
				_ => null
			};

		static XAttribute GetIncludeOrUpdateAttribute (XElement item)
			=> item.Attributes.FirstOrDefault (att => MSBuildElementSyntax.Item.GetAttribute (att.Name.FullName)?.SyntaxKind switch {
				MSBuildSyntaxKind.Item_Include => true,
				MSBuildSyntaxKind.Item_Update => true,
				_ => false
			});

		async Task<List<CompletionItem>> GetPackageVersionCompletions (MSBuildRootDocument doc, MSBuildResolveResult rr, CancellationToken token)
		{
			if (rr == null || GetItemGroupItemFromMetadata (rr) is not XElement itemEl || GetIncludeOrUpdateAttribute (itemEl) is not XAttribute includeAtt) {
				return null;
			}

			// we can only provide version completions if the item's value type is non-list nugetid
			var itemInfo = doc.GetSchemas ().GetItem (itemEl.Name.Name);
			if (itemInfo == null || !itemInfo.ValueKind.IsKindOrListOfKind (MSBuildValueKind.NuGetID)) {
				return null;
			}

			var packageType = itemInfo.CustomType?.Values[0].Name;

			var packageId = includeAtt.Value;
			if (string.IsNullOrEmpty (packageId)) {
				return null;
			}

			// check it's a non-list literal value, we can't handle anything else
			var expr = ExpressionParser.Parse (packageId, ExpressionOptions.ItemsMetadataAndLists);
			if (expr.NodeKind != ExpressionNodeKind.Text) {
				return null;
			}

			var tfm = doc.GetTargetFrameworkNuGetSearchParameter ();

			var results = await provider.PackageSearchManager.SearchPackageVersions (packageId.ToLower (), tfm, packageType).ToTask (token);

			//FIXME should we deduplicate?
			var items = new List<CompletionItem> ();
			var index = 0;
			foreach (var result in results.Reverse ()) {
				items.Add (CreateOrderedNuGetCompletionItem (result, XmlCompletionItemKind.AttributeValue, index++));
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

				foreach (var sdk in doc.Environment.GetRegisteredSdks ()) {
					if (sdks.Add (sdk.Name)) {
						items.Add (CreateSdkCompletionItem (sdk));
					}
				}

				//FIXME we should be able to cache these
				doc.Environment.ToolsetProperties.TryGetValue (WellKnownProperties.MSBuildSDKsPath, out var sdksPath);
				if (sdksPath != null) {
					AddSdksFromDir (sdksPath);
				}

				var dotNetSdk = doc.Environment.ResolveSdk (("Microsoft.NET.Sdk", null, null), null, null, Logger);
				if (dotNetSdk?.Path is string sdkPath) {
					string dotNetSdkPath = Path.GetDirectoryName (Path.GetDirectoryName (sdkPath));
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

		async Task<List<CompletionItem>> GetExpressionCompletionsAsync (
			IAsyncCompletionSession session,
			ITypedSymbol valueSymbol, TriggerState triggerState, ListKind listKind,
			int triggerLength, ExpressionNode triggerExpression,
			IReadOnlyList<ExpressionNode> comparandVariables,
			MSBuildResolveResult rr, SnapshotPoint triggerLocation,
			MSBuildRootDocument doc, CancellationToken token)
		{
			var kind = valueSymbol.InferValueKindIfUnknown ();

			if (!ValidateListPermitted (listKind, kind)) {
				return null;
			}

			bool allowExpressions = kind.AllowsExpressions ();

			kind = kind.WithoutModifiers ();

			if (kind == MSBuildValueKind.Data || kind == MSBuildValueKind.Nothing) {
				return null;
			}

			bool isValue = triggerState == TriggerState.Value;

			var items = new List<CompletionItem> ();

			if (comparandVariables != null && isValue) {
				foreach (var ci in ExpressionCompletion.GetComparandCompletions (doc, fileSystem, comparandVariables, Logger)) {
					items.Add (CreateCompletionItem (ci, XmlCompletionItemKind.AttributeValue));
				}
			}

			if (isValue) {
				switch (kind) {
				case MSBuildValueKind.NuGetID:
					if (triggerExpression is ExpressionText t) {
						var packageType = valueSymbol.CustomType?.Values[0].Name;
						var packageNameItems = await GetPackageNameCompletions (session, doc, t.Value, packageType, token);
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
				case MSBuildValueKind.Culture:
					items.AddRange (GetCultureCompletions ());
					break;
				}
			}

			//TODO: better metadata support
			IEnumerable<ISymbol> cinfos;
			if (valueSymbol.CustomType != null && valueSymbol.CustomType.Values.Count > 0 && isValue) {
				cinfos = valueSymbol.CustomType.Values;
			} else {
				//FIXME: can we avoid awaiting this unless we actually need to resolve a function? need to propagate async downwards
				await provider.FunctionTypeProvider.EnsureInitialized (token);
				cinfos = ExpressionCompletion.GetCompletionInfos (rr, triggerState, kind, triggerExpression, triggerLength, doc, provider.FunctionTypeProvider, fileSystem, Logger);
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

			return items;
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

			if (rr.AttributeSyntax != null) {
				switch (rr.AttributeSyntax.SyntaxKind) {
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

			if (rr.ElementSyntax != null) {
				switch (rr.ElementSyntax.SyntaxKind) {
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

		CompletionItem CreateOrderedNuGetCompletionItem (Tuple<string,FeedKind> info, XmlCompletionItemKind xmlCompletionItemKind, int index)
		{
			var kindImage = provider.DisplayElementFactory.GetImageElement (info.Item2);
			string displayText = info.Item1;
			var item = new CompletionItem (displayText, this, kindImage, ImmutableArray<CompletionFilter>.Empty, string.Empty, displayText, $"_{index:D5}", displayText, ImmutableArray<ImageElement>.Empty);
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
			if (info.Path is string sdkPath) {
				item.AddDocumentation (sdkPath);
			}

			return item;
		}

		IEnumerable<CompletionItem> GetLcidCompletions ()
		{
			var imageEl = provider.DisplayElementFactory.GetImageElement (KnownImages.Constant);
			foreach (var culture in CultureHelper.GetAllCultures ()) {
				string cultureLcid = culture.LCID.ToString ();
				string displayName = culture.DisplayName;
				// add the culture name to the filter text so ppl can just type the actual language/country instead of looking up the code
				string filterText = $"{cultureLcid} {displayName}";
				var item = new CompletionItem (cultureLcid, this, imageEl, ImmutableArray<CompletionFilter>.Empty, displayName, cultureLcid, cultureLcid, filterText, ImmutableArray<ImageElement>.Empty);
				item.Properties.AddProperty (typeof (ISymbol), CultureHelper.CreateLcidSymbol (culture));
				yield return item;
			}
		}

		IEnumerable<CompletionItem> GetCultureCompletions ()
		{
			var imageEl = provider.DisplayElementFactory.GetImageElement (KnownImages.Constant);
			foreach (var culture in CultureHelper.GetAllCultures ()) {
				string cultureName = culture.Name;
				if (string.IsNullOrEmpty (cultureName)) {
					continue;
				}
				// add the culture name to the filter text so ppl can just type the actual language/country instead of looking up the code
				string filterText = $"{culture.Name} {culture.DisplayName}";
				var item = new CompletionItem (culture.Name, this, imageEl, ImmutableArray<CompletionFilter>.Empty, culture.DisplayName, cultureName, cultureName, filterText, ImmutableArray<ImageElement>.Empty);
				item.Properties.AddProperty (typeof (ISymbol), CultureHelper.CreateCultureSymbol (culture));
				yield return item;
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
